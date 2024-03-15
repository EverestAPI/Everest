#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste;
using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Entities;
using Celeste.Mod.Helpers;
using Celeste.Mod.Meta;
using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Celeste {
    class patch_Level : Level {
        [MonoModIgnore]
        private enum ConditionBlockModes { }

        // This is used within Level.LoadEntity and Level.orig_LoadLevel so that entityData can be passed to ClutterBlocks being added to the scene.
        [ThreadStatic]
        internal static EntityData temporaryEntityData;

        // We're effectively in GameLoader, but still need to "expose" private fields to our mod.
        private static EventInstance PauseSnapshot;
        public static EventInstance _PauseSnapshot => PauseSnapshot;

        private static HashSet<string> _LoadStrings; // Generated in MonoModRules.PatchLevelLoader

        public SubHudRenderer SubHudRenderer;

        public class LoadOverride {

            public Player NextLoadedPlayer = null;
            public int SkipScreenWipes = 0;
            public bool ShouldAutoPause = false;

            public bool HasOverrides => NextLoadedPlayer != null || SkipScreenWipes != 0 || ShouldAutoPause;

        }

        private static readonly ConditionalWeakTable<Level, LoadOverride> LoadOverrides = new ConditionalWeakTable<Level, LoadOverride>();

        /// <summary>
        /// Registers an override of some level load parameters. Only one
        /// override can be registered for each level.
        /// </summary>
        /// <param name="level">The level for which to register the override</param>
        /// <param name="loadOverride">The override data to register</param>
        public static void RegisterLoadOverride(Level level, LoadOverride loadOverride) {
            if (loadOverride.HasOverrides)
                LoadOverrides.Add(level, loadOverride);
        }

        [Obsolete("Use RegisterLoadOverride instead")] public static Player NextLoadedPlayer;
        [Obsolete("Use RegisterLoadOverride instead")] public static int SkipScreenWipes;
        [Obsolete("Use RegisterLoadOverride instead")] public static bool ShouldAutoPause = false;

        public delegate Entity EntityLoader(Level level, LevelData levelData, Vector2 offset, EntityData entityData);
        public static readonly Dictionary<string, EntityLoader> EntityLoaders = new Dictionary<string, EntityLoader>();

        private float unpauseTimer;

        /// <summary>
        /// If in vanilla levels, gets the spawnpoint closest to the bottom left of the level.<br/>
        /// Otherwise, get the default spawnpoint from the level data if present, falling back to
        /// the first spawnpoint defined in the level data.
        /// </summary>
        public new Vector2 DefaultSpawnPoint {
            [MonoModReplace]
            get {
                if (Session.Area.GetLevelSet() == "Celeste")
                    return GetSpawnPoint(new Vector2(Bounds.Left, Bounds.Bottom));

                patch_LevelData levelData = (patch_LevelData) Session.LevelData;
                return levelData.DefaultSpawn ?? levelData.Spawns[0];
            }
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchLevelCanPause] // ... except for manually manipulating the method via MonoModRules
        public extern bool get_CanPause();

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchLevelRender] // ... except for manually manipulating the method via MonoModRules
        public override extern void Render();

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchLevelUpdate] // ... except for manually manipulating the method via MonoModRules
        public extern new void Update();

        [MonoModReplace]
        public new void RegisterAreaComplete() {
            bool completed = Completed;
            if (!completed) {
                Player player = base.Tracker.GetEntity<Player>();
                if (player != null) {
                    List<IStrawberry> strawbs = new List<IStrawberry>();
                    ReadOnlyCollection<Type> regBerries = StrawberryRegistry.GetBerryTypes();
                    foreach (Follower follower in player.Leader.Followers) {

                        if (regBerries.Contains(follower.Entity.GetType()) && follower.Entity is IStrawberry) {
                            strawbs.Add(follower.Entity as IStrawberry);
                        }
                    }
                    foreach (IStrawberry strawb in strawbs) {
                        strawb.OnCollect();
                    }
                }
                Completed = true;
                SaveData.Instance.RegisterCompletion(Session);
                Everest.Events.Level.Complete(this);
            }
        }

        /// <origdoc/>
        public extern void orig_DoScreenWipe(bool wipeIn, Action onComplete = null, bool hiresSnow = false);
        /// <summary>
        /// Activate the area-specific <see cref="ScreenWipe"/>.<br/>
        /// <seealso cref="AreaData.Wipe">See Also.</seealso><br/>
        /// If <see cref="SkipScreenWipes"/> is greater than zero, do nothing.
        /// </summary>
        /// <param name="wipeIn">Wipe direction.</param>
        /// <param name="onComplete"></param>
        /// <param name="hiresSnow"></param>
        public new void DoScreenWipe(bool wipeIn, Action onComplete = null, bool hiresSnow = false) {
            if (onComplete == null && !hiresSnow) {
                // Check if we should skip the screen wipe
#pragma warning disable 0618
                if (SkipScreenWipes > 0) {
                    SkipScreenWipes--;
                    return;
                }
#pragma warning restore 0618

                if (LoadOverrides.TryGetValue(this, out LoadOverride ovr) && ovr.SkipScreenWipes > 0) {
                    ovr.SkipScreenWipes--;
                    if (!ovr.HasOverrides)
                        LoadOverrides.Remove(this);
                    return;
                }
            }

            orig_DoScreenWipe(wipeIn, onComplete, hiresSnow);
        }

        // Needed for older mods.
        /// <inheritdoc cref="Level.NextColorGrade(string, float)"/>
        public void NextColorGrade(string next) {
            NextColorGrade(next, 1f);
        }

        /// <inheritdoc cref="Level.CompleteArea(bool, bool, bool)"/>
        public ScreenWipe CompleteArea(bool spotlightWipe = true, bool skipScreenWipe = false) {
            return CompleteArea(spotlightWipe, skipScreenWipe, false);
        }

        public extern void orig_Pause(int startIndex = 0, bool minimal = false, bool quickReset = false);
        public new void Pause(int startIndex = 0, bool minimal = false, bool quickReset = false) {
            orig_Pause(startIndex, minimal, quickReset);

            if (((patch_EntityList) (object) Entities).ToAdd.FirstOrDefault(e => e is TextMenu) is patch_TextMenu menu) {
                void Unpause() {
                    Everest.Events.Level.Unpause(this);
                }
                menu.OnPause += Unpause;
                menu.OnESC += Unpause;
                if (!quickReset) {
                    menu.OnCancel += Unpause; // the normal pause menu unpauses for all three of these, the quick reset menu does not
                    Everest.Events.Level.CreatePauseMenuButtons(this, menu, minimal);
                }
            }

            Everest.Events.Level.Pause(this, startIndex, minimal, quickReset);
        }

        public extern void orig_TransitionTo(LevelData next, Vector2 direction);
        public new void TransitionTo(LevelData next, Vector2 direction) {
            orig_TransitionTo(next, direction);
            Everest.Events.Level.TransitionTo(this, next, direction);
        }

        [PatchTransitionRoutine]
        private extern IEnumerator orig_TransitionRoutine(LevelData next, Vector2 direction);
        private IEnumerator TransitionRoutine(LevelData next, Vector2 direction) {
            Player player = Tracker.GetEntity<Player>();
            if (player == null) {
                // Blame Rubydragon for performing a frame-perfect transition death during a speedrun :P -ade
                Engine.Scene = new LevelLoader(Session, Session.RespawnPoint);
                yield break;
            }

            IEnumerator orig = orig_TransitionRoutine(next, direction);

            // Don't perform any GBJ checks in vanilla maps.
            if (Session.Area.GetLevelSet() == "Celeste" || CoreModule.Settings.DisableAntiSoftlock) {
                while (orig.MoveNext())
                    yield return orig.Current;
                yield break;
            }

            player = Tracker.GetEntity<Player>();
            // No player? No problem!
            if (player == null) {
                while (orig.MoveNext())
                    yield return orig.Current;
                yield break;
            }

            Vector2 playerPos = player.Position;
            TimeSpan playerStuck = TimeSpan.FromTicks(Session.Time);

            while (orig.MoveNext()) {
                if (playerPos != player.Position)
                    playerStuck = TimeSpan.FromTicks(Session.Time);
                playerPos = player.Position;

                if ((TimeSpan.FromTicks(Session.Time) - playerStuck).TotalSeconds >= 5D) {
                    // Player stuck in GBJ - force-reload the level.
                    Session.Level = next.Name;
                    Session.RespawnPoint = Session.LevelData.Spawns.ClosestTo(player.Position);
                    Engine.Scene = new LevelLoader(Session, Session.RespawnPoint);
                    yield break;
                }

                yield return orig.Current;
            }
        }

        [PatchLevelLoader] // Manually manipulate the method via MonoModRules
        [PatchLevelLoaderDecalCreation]
        public extern void orig_LoadLevel(Player.IntroTypes playerIntro, bool isFromLoader = false);
        public new void LoadLevel(Player.IntroTypes playerIntro, bool isFromLoader = false) {
            // Read player introType from metadata as player enter the C-Side
            if (Session.FirstLevel && Session.StartedFromBeginning && Session.JustStarted
                && (!(Engine.Scene is LevelLoader loader) || !loader.PlayerIntroTypeOverride.HasValue)
                && Session.Area.Mode == AreaMode.CSide
                && (AreaData.GetMode(Session.Area) as patch_ModeProperties)?.MapMeta is MapMeta mapMeta && (mapMeta.OverrideASideMeta ?? false)
                && mapMeta.IntroType is Player.IntroTypes introType)
                playerIntro = introType;

            try {
                Logger.Log(LogLevel.Verbose, "LoadLevel", $"Loading room {Session.LevelData.Name} of {Session.Area.GetSID()}");

                orig_LoadLevel(playerIntro, isFromLoader);

                // Check if we should auto-pause
#pragma warning disable 0618
                if (ShouldAutoPause) {
                    ShouldAutoPause = false;
                    Pause();
                }
#pragma warning restore 0618

                if (LoadOverrides.TryGetValue(this, out LoadOverride ovr) && ovr.ShouldAutoPause) {
                    ovr.ShouldAutoPause = false;
                    if (!ovr.HasOverrides)
                        LoadOverrides.Remove(this);

                    Pause();
                }
            } catch (Exception e) {
                if (patch_LevelEnter.ErrorMessage == null) {
                    if (e is ArgumentOutOfRangeException && e.MethodInStacktrace(typeof(Level), "get_DefaultSpawnPoint")) {
                        patch_LevelEnter.ErrorMessage = Dialog.Get("postcard_levelnospawn");
                    } else {
                        patch_LevelEnter.ErrorMessage = Dialog.Get("postcard_levelloadfailed").Replace("((sid))", Session.Area.GetSID());
                    }
                }

                Logger.Log(LogLevel.Warn, "LoadLevel", $"Failed loading room {Session.Level} of {Session.Area.GetSID()}");
                e.LogDetailed();
                return;
            }
            Everest.Events.Level.LoadLevel(this, playerIntro, isFromLoader);
        }

        internal AreaMode _PatchHeartGemBehavior(AreaMode levelMode) {
            if (Session.Area.GetLevelSet() == "Celeste") {
                // do not mess with vanilla.
                return levelMode;
            }

            MapMetaModeProperties properties = ((patch_MapData) Session.MapData).Meta;
            if (properties != null && (properties.HeartIsEnd ?? false)) {
                // heart ends the level: this is like B-Sides.
                // the heart will appear even if it was collected, to avoid a softlock if we save & quit after collecting it.
                return AreaMode.BSide;
            } else {
                // heart does not end the level: this is like A-Sides.
                // the heart will disappear after it is collected.
                return AreaMode.Normal;
            }
        }

        [ThreadStatic] private static Player _PlayerOverride;

        [Obsolete("Use LoadNewPlayerForLevel instead")] // Some mods hook this method ._.
        private static Player LoadNewPlayer(Vector2 position, PlayerSpriteMode spriteMode) {
            if (_PlayerOverride != null)
                return _PlayerOverride;

#pragma warning disable 0618
            Player player = NextLoadedPlayer;
            if (player != null) {
                NextLoadedPlayer = null;
                return player;
            }
#pragma warning restore 0618

            return new Player(position, spriteMode);
        }
        // Called from LoadLevel, patched via MonoModRules.PatchLevelLoader
        private static Player LoadNewPlayerForLevel(Vector2 position, PlayerSpriteMode spriteMode, Level lvl) {
            // Check if there is a player override
            if (LoadOverrides.TryGetValue(lvl, out LoadOverride ovr) && ovr.NextLoadedPlayer != null) {
                Player player = ovr.NextLoadedPlayer;

                // Oh wait, you think we can just return the player override now?
                // Some mods might depend on the old method being called! ._.
                // (They might also depend on the exact semantics of NextLoadedPlayer holding the new player, but screw them in that case)
                // (Their fault for hooking into a private Everest-internal method)
#pragma warning disable 0618
                try {
                    _PlayerOverride = player;

                    Player actualPlayer = LoadNewPlayer(position, spriteMode);
                    if (actualPlayer != player)
                        return actualPlayer;
                } finally {
                    _PlayerOverride = null;
                }
#pragma warning restore 0618

                // The old method didn't object, actually apply the override now
                ovr.NextLoadedPlayer = null;

                if (!ovr.HasOverrides)
                    LoadOverrides.Remove(lvl);

                return player;
            }

            // Fall back to the obsolete overload
#pragma warning disable 0618
            return LoadNewPlayer(position, spriteMode);
#pragma warning restore 0618
        }
      
        public static bool LoadCustomEntity(EntityData entityData, Level level) => LoadCustomEntity(entityData, level, null, null);

        /// <summary>
        /// Search for a custom entity that matches the <see cref="EntityData.Name"/>.<br/>
        /// To register a custom entity, use <see cref="CustomEntityAttribute"/> or <see cref="Everest.Events.Level.OnLoadEntity"/>.<br/>
        /// <seealso href="https://github.com/EverestAPI/Resources/wiki/Custom-Entities-and-Triggers">Read More</seealso>.
        /// </summary>
        /// <param name="entityData"></param>
        /// <param name="level">The level to add the entity to.</param>
        /// <param name="levelData">If you want to set a specific LevelData for the Entity spawn to, do so here.</param>
        /// <param name="roomOffset">If you want to set a specific World Offset for the Entity to spawn at, do so here.</param>
        /// <returns></returns>
        public static bool LoadCustomEntity(EntityData entityData, Level level, LevelData levelData = null, Vector2? roomOffset = null) {
            levelData ??= level.Session.LevelData;
            Vector2 offset = roomOffset ?? new Vector2(levelData.Bounds.Left, levelData.Bounds.Top);

            if (Everest.Events.Level.LoadEntity(level, levelData, offset, entityData))
                return true;

            if (EntityLoaders.TryGetValue(entityData.Name, out EntityLoader loader)) {
                Entity loaded = loader(level, levelData, offset, entityData);
                if (loaded != null) {
                    level.Add(loaded);
                    return true;
                }
            }

            if (entityData.Name == "everest/spaceController") {
                level.Add(new SpaceController());
                return true;
            }

            // The following entities have hardcoded "attributes."
            // Everest allows custom maps to set them.

            if (entityData.Name == "spinner") {
                if (level.Session.Area.ID == 3 ||
                    (level.Session.Area.ID == 7 && level.Session.Level.StartsWith("d-")) ||
                    entityData.Bool("dust")) {
                    level.Add(new DustStaticSpinner(entityData, offset));
                    return true;
                }

                CrystalColor color = CrystalColor.Blue;
                if (level.Session.Area.ID == 5)
                    color = CrystalColor.Red;
                else if (level.Session.Area.ID == 6)
                    color = CrystalColor.Purple;
                else if (level.Session.Area.ID == 10)
                    color = CrystalColor.Rainbow;
                else if ("core".Equals(entityData.Attr("color"), StringComparison.InvariantCultureIgnoreCase))
                    color = (CrystalColor) (-1);
                else if (!Enum.TryParse(entityData.Attr("color"), true, out color))
                    color = CrystalColor.Blue;

                level.Add(new CrystalStaticSpinner(entityData, offset, color));
                return true;
            }

            if (entityData.Name == "trackSpinner") {
                if (level.Session.Area.ID == 10 ||
                    entityData.Bool("star")) {
                    level.Add(new StarTrackSpinner(entityData, offset));
                    return true;
                } else if (level.Session.Area.ID == 3 ||
                    (level.Session.Area.ID == 7 && level.Session.Level.StartsWith("d-")) ||
                    entityData.Bool("dust")) {
                    level.Add(new DustTrackSpinner(entityData, offset));
                    return true;
                }

                level.Add(new BladeTrackSpinner(entityData, offset));
                return true;
            }

            if (entityData.Name == "rotateSpinner") {
                if (level.Session.Area.ID == 10 ||
                    entityData.Bool("star")) {
                    level.Add(new StarRotateSpinner(entityData, offset));
                    return true;
                } else if (level.Session.Area.ID == 3 ||
                    (level.Session.Area.ID == 7 && level.Session.Level.StartsWith("d-")) ||
                    entityData.Bool("dust")) {
                    level.Add(new DustRotateSpinner(entityData, offset));
                    return true;
                }

                level.Add(new BladeRotateSpinner(entityData, offset));
                return true;
            }

            if (entityData.Name == "checkpoint" &&
                entityData.Position == Vector2.Zero &&
                !entityData.Bool("allowOrigin")) {
                // Workaround for mod levels with old versions of Ahorn containing a checkpoint at (0, 0):
                // Create the checkpoint and avoid the start position update in orig_Load.
                level.Add(new Checkpoint(entityData, offset));
                return true;
            }

            if (entityData.Name == "cloud") {
                patch_Cloud cloud = new Cloud(entityData, offset) as patch_Cloud;
                if (entityData.Has("small"))
                    cloud.Small = entityData.Bool("small");
                level.Add(cloud);
                return true;
            }

            if (entityData.Name == "cobweb") {
                patch_Cobweb cobweb = new Cobweb(entityData, offset) as patch_Cobweb;
                if (entityData.Has("color"))
                    cobweb.OverrideColors = entityData.Attr("color")?.Split(',').Select(s => Calc.HexToColor(s)).ToArray();
                level.Add(cobweb);
                return true;
            }

            if (entityData.Name == "movingPlatform") {
                patch_MovingPlatform platform = new MovingPlatform(entityData, offset) as patch_MovingPlatform;
                if (entityData.Has("texture"))
                    platform.OverrideTexture = entityData.Attr("texture");
                level.Add(platform);
                return true;
            }

            if (entityData.Name == "sinkingPlatform") {
                patch_SinkingPlatform platform = new SinkingPlatform(entityData, offset) as patch_SinkingPlatform;
                if (entityData.Has("texture"))
                    platform.OverrideTexture = entityData.Attr("texture");
                level.Add(platform);
                return true;
            }

            if (entityData.Name == "crumbleBlock") {
                patch_CrumblePlatform platform = new CrumblePlatform(entityData, offset) as patch_CrumblePlatform;
                if (entityData.Has("texture"))
                    platform.OverrideTexture = entityData.Attr("texture");
                level.Add(platform);
                return true;
            }

            if (entityData.Name == "wire") {
                Wire wire = new Wire(entityData, offset);
                if (entityData.Has("color"))
                    wire.Color = entityData.HexColor("color");
                level.Add(wire);
                return true;
            }

            if (!_LoadStrings.Contains(entityData.Name)) {
                Logger.Log(LogLevel.Warn, "LoadLevel", $"Failed loading entity {entityData.Name}. Room: {entityData.Level.Name} Position: {entityData.Position}");
            }

            return false;
        }

        private static object _GCCollectLock = Tuple.Create(new object(), "Level Transition GC.Collect");
        private static void _GCCollect() {
            if (!(CoreModule.Settings.MultithreadedGC ?? !Everest.Flags.IsMono)) {
                GC.Collect();
                return;
            }

            QueuedTaskHelper.Do(_GCCollectLock, () => {
                GC.Collect(1, GCCollectionMode.Forced, false);
            });
        }

        public extern Vector2 orig_GetFullCameraTargetAt(Player player, Vector2 at);
        public new Vector2 GetFullCameraTargetAt(Player player, Vector2 at) {
            Vector2 originalPosition = player.Position;
            player.Position = at;
            foreach (Entity trigger in Tracker.GetEntities<Trigger>()) {
                if (trigger is SmoothCameraOffsetTrigger smoothCameraOffset && player.CollideCheck(trigger)) {
                    smoothCameraOffset.OnStay(player);
                }
            }
            player.Position = originalPosition;

            return orig_GetFullCameraTargetAt(player, at);
        }

        public extern void orig_End();
        public override void End() {
            orig_End();

            // if we are not entering PICO-8 or the Reflection Fall cutscene...
            if (!(patch_Engine.NextScene is Pico8.Emulator) && !(patch_Engine.NextScene is OverworldReflectionsFall)) {
                // break all links between this level and its entities.
                foreach (Entity entity in Entities) {
                    ((patch_Entity) entity).DissociateFromScene();
                }
                ((patch_EntityList) (object) Entities).ClearEntities();
            }
        }

        public Vector2 ScreenToWorld(Vector2 position) {
            Vector2 size = new Vector2(320f, 180f);
            Vector2 scaledSize = size / ZoomTarget;
            Vector2 offset = ZoomTarget != 1f ? (ZoomFocusPoint - scaledSize / 2f) / (size - scaledSize) * size : Vector2.Zero;
            float scale = Zoom * ((320f - ScreenPadding * 2f) / 320f);
            Vector2 paddingOffset = new Vector2(ScreenPadding, ScreenPadding * 9f / 16f);

            if (SaveData.Instance?.Assists.MirrorMode ?? false) {
                position.X = 1920f - position.X;
            }
            position /= 1920f / 320f;
            position -= paddingOffset;
            position = (position - offset) / scale + offset;
            position = Camera.ScreenToCamera(position);
            return position;
        }

        public Vector2 WorldToScreen(Vector2 position) {
            Vector2 size = new Vector2(320f, 180f);
            Vector2 scaledSize = size / ZoomTarget;
            Vector2 offset = ZoomTarget != 1f ? (ZoomFocusPoint - scaledSize / 2f) / (size - scaledSize) * size : Vector2.Zero;
            float scale = Zoom * ((320f - ScreenPadding * 2f) / 320f);
            Vector2 paddingOffset = new Vector2(ScreenPadding, ScreenPadding * 9f / 16f);

            position = Camera.CameraToScreen(position);
            position = (position - offset) * scale + offset;
            position += paddingOffset;
            position *= 1920f / 320f;
            if (SaveData.Instance?.Assists.MirrorMode ?? false) {
                position.X = 1920f - position.X;
            }
            return position;
        }

        private void FixChaserStatesTimeStamp() {
            if (Session.Area.GetLevelSet() != "Celeste" && unpauseTimer > 0f && Tracker.GetEntity<Player>()?.ChaserStates is { } chaserStates) {
                float offset = Engine.DeltaTime;

                // add one more frame at the end
                if (unpauseTimer - Engine.RawDeltaTime <= 0f)
                    offset *= 2;

                for (int i = 0; i < chaserStates.Count; i++) {
                    Player.ChaserState chaserState = chaserStates[i];
                    chaserState.TimeStamp += offset;
                    chaserStates[i] = chaserState;
                }
            }
        }

        private bool CheckForErrors() {
            bool errorPresent = patch_LevelEnter.ErrorMessage != null;
            if (errorPresent) {
                LevelEnter.Go(Session, false);
            }

            return errorPresent;
        }

        private bool _IsInDoNotLoadIncreased(LevelData level, EntityData entity) => Session.DoNotLoad.Contains(new EntityID(level.Name, entity.ID + 20000000));

        [MonoModIgnore]
        internal extern bool GotCollectables(EntityData data);
    }

    public static class LevelExt {

        internal static EventInstance PauseSnapshot => patch_Level._PauseSnapshot;

        [Obsolete("Use Level.SubHudRenderer instead.")]
        public static SubHudRenderer GetSubHudRenderer(this Level self)
            => ((patch_Level) self).SubHudRenderer;
        [Obsolete("Use Level.SubHudRenderer instead.")]
        public static void SetSubHudRenderer(this Level self, SubHudRenderer value)
            => ((patch_Level) self).SubHudRenderer = value;

        /// <summary>
        /// Loads an Entity into the Level
        /// </summary>
        /// <param name="entity3">the EntityData for the Entity.</param>
        /// <param name="levelData">Optional; the Level (note: room) the entity should be added into. You shouldn't use this unless your entity is global.</param>
        /// <param name="roomOffset">Optional; used for setting the relative position of the entity</param>
        /// <returns>Whether or not the entity successfully loaded</returns>
        public static bool LoadEntity(this Level self, EntityData entity3, LevelData levelData = null, Vector2? roomOffset = null) {
            patch_Level.temporaryEntityData = entity3;
            levelData ??= self.Session.LevelData;
            entity3.Level = levelData;
            int iD = entity3.ID;
            EntityID entityID = new EntityID(levelData.Name, iD);
            if (self.Session.DoNotLoad.Contains(entityID)) {
                patch_Level.temporaryEntityData = null;
                return false;
            } else if (patch_Level.LoadCustomEntity(entity3, self, levelData, roomOffset)) {
                patch_Level.temporaryEntityData = null;
                return true;
            } else {

                Vector2 vector = roomOffset ?? new Vector2(levelData.Bounds.Left, levelData.Bounds.Top);
                switch (entity3.Name) {
                    case "jumpThru":
                        self.Add(new JumpthruPlatform(entity3, vector));
                        break;
                    case "refill":
                        self.Add(new Refill(entity3, vector));
                        break;
                    case "infiniteStar":
                        self.Add(new FlyFeather(entity3, vector));
                        break;
                    case "strawberry":
                        self.Add(new Strawberry(entity3, vector, entityID));
                        break;
                    case "memorialTextController":
                        if (self.Session.Dashes == 0 && (self.Session.StartedFromBeginning || (self.Session as patch_Session).RestartedFromGolden)) {
                            self.Add(new Strawberry(entity3, vector, entityID));
                        }
                        break;
                    case "goldenBerry": {
                            bool cheatMode = SaveData.Instance.CheatMode;
                            bool flag4 = self.Session.FurthestSeenLevel == self.Session.Level || self.Session.Deaths == 0;
                            bool flag5 = SaveData.Instance.UnlockedModes >= 3 || SaveData.Instance.DebugMode;
                            bool completed = (SaveData.Instance as patch_SaveData).Areas_Safe[self.Session.Area.ID].Modes[(int) self.Session.Area.Mode].Completed;
                            if ((cheatMode || (flag5 && completed)) && flag4) {
                                self.Add(new Strawberry(entity3, vector, entityID));
                            }
                            break;
                        }
                    case "summitgem":
                        self.Add(new SummitGem(entity3, vector, entityID));
                        break;
                    case "blackGem":
                        if (!self.Session.HeartGem || (self as patch_Level)._PatchHeartGemBehavior(self.Session.Area.Mode) != 0) {
                            self.Add(new HeartGem(entity3, vector));
                        }
                        break;
                    case "dreamHeartGem":
                        if (!self.Session.HeartGem) {
                            self.Add(new DreamHeartGem(entity3, vector));
                        }
                        break;
                    case "spring":
                        self.Add(new Spring(entity3, vector, Spring.Orientations.Floor));
                        break;
                    case "wallSpringLeft":
                        self.Add(new Spring(entity3, vector, Spring.Orientations.WallLeft));
                        break;
                    case "wallSpringRight":
                        self.Add(new Spring(entity3, vector, Spring.Orientations.WallRight));
                        break;
                    case "fallingBlock":
                        self.Add(new FallingBlock(entity3, vector));
                        break;
                    case "zipMover":
                        self.Add(new ZipMover(entity3, vector));
                        break;
                    case "crumbleBlock":
                        self.Add(new CrumblePlatform(entity3, vector));
                        break;
                    case "dreamBlock":
                        self.Add(new DreamBlock(entity3, vector));
                        break;
                    case "touchSwitch":
                        self.Add(new TouchSwitch(entity3, vector));
                        break;
                    case "switchGate":
                        self.Add(new SwitchGate(entity3, vector));
                        break;
                    case "negaBlock":
                        self.Add(new NegaBlock(entity3, vector));
                        break;
                    case "key":
                        self.Add(new Key(entity3, vector, entityID));
                        break;
                    case "lockBlock":
                        self.Add(new LockBlock(entity3, vector, entityID));
                        break;
                    case "movingPlatform":
                        self.Add(new MovingPlatform(entity3, vector));
                        break;
                    case "rotatingPlatforms": {
                            Vector2 vector2 = entity3.Position + vector;
                            Vector2 vector3 = entity3.Nodes[0] + vector;
                            int width = entity3.Width;
                            int num2 = entity3.Int("platforms");
                            bool clockwise = entity3.Bool("clockwise");
                            float length = (vector2 - vector3).Length();
                            float num3 = (vector2 - vector3).Angle();
                            float num4 = (float) Math.PI * 2f / (float) num2;
                            for (int j = 0; j < num2; j++) {
                                float angleRadians = num3 + num4 * (float) j;
                                angleRadians = Calc.WrapAngle(angleRadians);
                                Vector2 position2 = vector3 + Calc.AngleToVector(angleRadians, length);
                                self.Add(new RotatingPlatform(position2, width, vector3, clockwise));
                            }
                            break;
                        }
                    case "blockField":
                        self.Add(new BlockField(entity3, vector));
                        break;
                    case "cloud":
                        self.Add(new Cloud(entity3, vector));
                        break;
                    case "booster":
                        self.Add(new Booster(entity3, vector));
                        break;
                    case "moveBlock":
                        self.Add(new MoveBlock(entity3, vector));
                        break;
                    case "light":
                        self.Add(new PropLight(entity3, vector));
                        break;
                    case "switchBlock":
                    case "swapBlock":
                        self.Add(new SwapBlock(entity3, vector));
                        break;
                    case "dashSwitchH":
                    case "dashSwitchV":
                        self.Add(DashSwitch.Create(entity3, vector, entityID));
                        break;
                    case "templeGate":
                        self.Add(new TempleGate(entity3, vector, levelData.Name));
                        break;
                    case "torch":
                        self.Add(new Torch(entity3, vector, entityID));
                        break;
                    case "templeCrackedBlock":
                        self.Add(new TempleCrackedBlock(entityID, entity3, vector));
                        break;
                    case "seekerBarrier":
                        self.Add(new SeekerBarrier(entity3, vector));
                        break;
                    case "theoCrystal":
                        self.Add(new TheoCrystal(entity3, vector));
                        break;
                    case "glider":
                        self.Add(new Glider(entity3, vector));
                        break;
                    case "theoCrystalPedestal":
                        self.Add(new TheoCrystalPedestal(entity3, vector));
                        break;
                    case "badelineBoost":
                        self.Add(new BadelineBoost(entity3, vector));
                        break;
                    case "cassette":
                        if (!self.Session.Cassette) {
                            self.Add(new Cassette(entity3, vector));
                        }
                        break;
                    case "cassetteBlock": {
                            CassetteBlock cassetteBlock = new CassetteBlock(entity3, vector, entityID);
                            self.Add(cassetteBlock);
                            self.HasCassetteBlocks = true;
                            if (self.CassetteBlockTempo == 1f) {
                                self.CassetteBlockTempo = cassetteBlock.Tempo;
                            }
                            self.CassetteBlockBeats = Math.Max(cassetteBlock.Index + 1, self.CassetteBlockBeats);
                            if (self.Tracker.GetEntity<CassetteBlockManager>() == null && (self.Session.Area.Mode != AreaMode.Normal || !self.Session.Cassette)) {
                                self.Add(new CassetteBlockManager());
                            }
                            break;
                        }
                    case "wallBooster":
                        self.Add(new WallBooster(entity3, vector));
                        break;
                    case "bounceBlock":
                        self.Add(new BounceBlock(entity3, vector));
                        break;
                    case "coreModeToggle":
                        self.Add(new CoreModeToggle(entity3, vector));
                        break;
                    case "iceBlock":
                        self.Add(new IceBlock(entity3, vector));
                        break;
                    case "fireBarrier":
                        self.Add(new FireBarrier(entity3, vector));
                        break;
                    case "eyebomb":
                        self.Add(new Puffer(entity3, vector));
                        break;
                    case "flingBird":
                        self.Add(new FlingBird(entity3, vector));
                        break;
                    case "flingBirdIntro":
                        self.Add(new FlingBirdIntro(entity3, vector));
                        break;
                    case "birdPath":
                        self.Add(new BirdPath(entityID, entity3, vector));
                        break;
                    case "lightningBlock":
                        self.Add(new LightningBreakerBox(entity3, vector));
                        break;
                    case "spikesUp":
                        self.Add(new Spikes(entity3, vector, Spikes.Directions.Up));
                        break;
                    case "spikesDown":
                        self.Add(new Spikes(entity3, vector, Spikes.Directions.Down));
                        break;
                    case "spikesLeft":
                        self.Add(new Spikes(entity3, vector, Spikes.Directions.Left));
                        break;
                    case "spikesRight":
                        self.Add(new Spikes(entity3, vector, Spikes.Directions.Right));
                        break;
                    case "triggerSpikesUp":
                        self.Add(new TriggerSpikes(entity3, vector, TriggerSpikes.Directions.Up));
                        break;
                    case "triggerSpikesDown":
                        self.Add(new TriggerSpikes(entity3, vector, TriggerSpikes.Directions.Down));
                        break;
                    case "triggerSpikesRight":
                        self.Add(new TriggerSpikes(entity3, vector, TriggerSpikes.Directions.Right));
                        break;
                    case "triggerSpikesLeft":
                        self.Add(new TriggerSpikes(entity3, vector, TriggerSpikes.Directions.Left));
                        break;
                    case "darkChaser":
                        self.Add(new BadelineOldsite(entity3, vector, self.Tracker.CountEntities<BadelineOldsite>()));
                        break;
                    case "rotateSpinner":
                        if (self.Session.Area.ID == 10) {
                            self.Add(new StarRotateSpinner(entity3, vector));
                        } else if (self.Session.Area.ID == 3 || (self.Session.Area.ID == 7 && self.Session.Level.StartsWith("d-"))) {
                            self.Add(new DustRotateSpinner(entity3, vector));
                        } else {
                            self.Add(new BladeRotateSpinner(entity3, vector));
                        }
                        break;
                    case "trackSpinner":
                        if (self.Session.Area.ID == 10) {
                            self.Add(new StarTrackSpinner(entity3, vector));
                        } else if (self.Session.Area.ID == 3 || (self.Session.Area.ID == 7 && self.Session.Level.StartsWith("d-"))) {
                            self.Add(new DustTrackSpinner(entity3, vector));
                        } else {
                            self.Add(new BladeTrackSpinner(entity3, vector));
                        }
                        break;
                    case "spinner": {
                            if (self.Session.Area.ID == 3 || (self.Session.Area.ID == 7 && self.Session.Level.StartsWith("d-"))) {
                                self.Add(new DustStaticSpinner(entity3, vector));
                                break;
                            }
                            CrystalColor color = CrystalColor.Blue;
                            if (self.Session.Area.ID == 5) {
                                color = CrystalColor.Red;
                            } else if (self.Session.Area.ID == 6) {
                                color = CrystalColor.Purple;
                            } else if (self.Session.Area.ID == 10) {
                                color = CrystalColor.Rainbow;
                            }
                            self.Add(new CrystalStaticSpinner(entity3, vector, color));
                            break;
                        }
                    case "sinkingPlatform":
                        self.Add(new SinkingPlatform(entity3, vector));
                        break;
                    case "friendlyGhost":
                        self.Add(new AngryOshiro(entity3, vector));
                        break;
                    case "seeker":
                        self.Add(new Seeker(entity3, vector));
                        break;
                    case "seekerStatue":
                        self.Add(new SeekerStatue(entity3, vector));
                        break;
                    case "slider":
                        self.Add(new Slider(entity3, vector));
                        break;
                    case "templeBigEyeball":
                        self.Add(new TempleBigEyeball(entity3, vector));
                        break;
                    case "crushBlock":
                        self.Add(new CrushBlock(entity3, vector));
                        break;
                    case "bigSpinner":
                        self.Add(new Bumper(entity3, vector));
                        break;
                    case "starJumpBlock":
                        self.Add(new StarJumpBlock(entity3, vector));
                        break;
                    case "floatySpaceBlock":
                        self.Add(new FloatySpaceBlock(entity3, vector));
                        break;
                    case "glassBlock":
                        self.Add(new GlassBlock(entity3, vector));
                        break;
                    case "goldenBlock":
                        self.Add(new GoldenBlock(entity3, vector));
                        break;
                    case "fireBall":
                        self.Add(new FireBall(entity3, vector));
                        break;
                    case "risingLava":
                        self.Add(new RisingLava(entity3, vector));
                        break;
                    case "sandwichLava":
                        self.Add(new SandwichLava(entity3, vector));
                        break;
                    case "killbox":
                        self.Add(new Killbox(entity3, vector));
                        break;
                    case "fakeHeart":
                        self.Add(new FakeHeart(entity3, vector));
                        break;
                    case "lightning":
                        if (entity3.Bool("perLevel") || !self.Session.GetFlag("disable_lightning")) {
                            self.Add(new Lightning(entity3, vector));
                        }
                        break;
                    case "finalBoss":
                        self.Add(new FinalBoss(entity3, vector));
                        break;
                    case "finalBossFallingBlock":
                        self.Add(FallingBlock.CreateFinalBossBlock(entity3, vector));
                        break;
                    case "finalBossMovingBlock":
                        self.Add(new FinalBossMovingBlock(entity3, vector));
                        break;
                    case "fakeWall":
                        self.Add(new FakeWall(entityID, entity3, vector, FakeWall.Modes.Wall));
                        break;
                    case "fakeBlock":
                        self.Add(new FakeWall(entityID, entity3, vector, FakeWall.Modes.Block));
                        break;
                    case "dashBlock":
                        self.Add(new DashBlock(entity3, vector, entityID));
                        break;
                    case "invisibleBarrier":
                        self.Add(new InvisibleBarrier(entity3, vector));
                        break;
                    case "exitBlock":
                        self.Add(new ExitBlock(entity3, vector));
                        break;
                    case "conditionBlock": {
                            string conditionBlockModes = entity3.Attr("condition", "Key");
                            EntityID none = EntityID.None;
                            string[] array = entity3.Attr("conditionID").Split(':');
                            none.Level = array[0];
                            none.ID = Convert.ToInt32(array[1]);
                            if (conditionBlockModes.ToLowerInvariant() switch {
                                "button" => self.Session.GetFlag(DashSwitch.GetFlagName(none)),
                                "key" => self.Session.DoNotLoad.Contains(none),
                                "strawberry" => self.Session.Strawberries.Contains(none),
                                _ => throw new Exception("Condition type not supported!"),
                            }) {
                                self.Add(new ExitBlock(entity3, vector));
                            }
                            break;
                        }
                    case "coverupWall":
                        self.Add(new CoverupWall(entity3, vector));
                        break;
                    case "crumbleWallOnRumble":
                        self.Add(new CrumbleWallOnRumble(entity3, vector, entityID));
                        break;
                    case "ridgeGate":
                        if ((self as patch_Level).GotCollectables(entity3)) {
                            self.Add(new RidgeGate(entity3, vector));
                        }
                        break;
                    case "tentacles":
                        self.Add(new ReflectionTentacles(entity3, vector));
                        break;
                    case "starClimbController":
                        self.Add(new StarJumpController());
                        break;
                    case "playerSeeker":
                        self.Add(new PlayerSeeker(entity3, vector));
                        break;
                    case "chaserBarrier":
                        self.Add(new ChaserBarrier(entity3, vector));
                        break;
                    case "introCrusher":
                        self.Add(new IntroCrusher(entity3, vector));
                        break;
                    case "bridge":
                        self.Add(new Bridge(entity3, vector));
                        break;
                    case "bridgeFixed":
                        self.Add(new BridgeFixed(entity3, vector));
                        break;
                    case "bird":
                        self.Add(new BirdNPC(entity3, vector));
                        break;
                    case "introCar":
                        self.Add(new IntroCar(entity3, vector));
                        break;
                    case "memorial":
                        self.Add(new Memorial(entity3, vector));
                        break;
                    case "wire":
                        self.Add(new Wire(entity3, vector));
                        break;
                    case "cobweb":
                        self.Add(new Cobweb(entity3, vector));
                        break;
                    case "lamp":
                        self.Add(new Lamp(vector + entity3.Position, entity3.Bool("broken")));
                        break;
                    case "hanginglamp":
                        self.Add(new HangingLamp(entity3, vector + entity3.Position));
                        break;
                    case "hahaha":
                        self.Add(new Hahaha(entity3, vector));
                        break;
                    case "bonfire":
                        self.Add(new Bonfire(entity3, vector));
                        break;
                    case "payphone":
                        self.Add(new Payphone(vector + entity3.Position));
                        break;
                    case "colorSwitch":
                        self.Add(new ClutterSwitch(entity3, vector));
                        break;
                    case "clutterDoor":
                        self.Add(new ClutterDoor(entity3, vector, self.Session));
                        break;
                    case "dreammirror":
                        self.Add(new DreamMirror(vector + entity3.Position));
                        break;
                    case "resortmirror":
                        self.Add(new ResortMirror(entity3, vector));
                        break;
                    case "towerviewer":
                        self.Add(new Lookout(entity3, vector));
                        break;
                    case "picoconsole":
                        self.Add(new PicoConsole(entity3, vector));
                        break;
                    case "wavedashmachine":
                        self.Add(new WaveDashTutorialMachine(entity3, vector));
                        break;
                    case "yellowBlocks":
                        ClutterBlockGenerator.Init(self);
                        patch_Level.temporaryEntityData = entity3;
                        ClutterBlockGenerator.Add((int) (entity3.Position.X / 8f), (int) (entity3.Position.Y / 8f), entity3.Width / 8, entity3.Height / 8, ClutterBlock.Colors.Yellow);
                        patch_Level.temporaryEntityData = null;
                        break;
                    case "redBlocks":
                        ClutterBlockGenerator.Init(self);
                        patch_Level.temporaryEntityData = entity3;
                        ClutterBlockGenerator.Add((int) (entity3.Position.X / 8f), (int) (entity3.Position.Y / 8f), entity3.Width / 8, entity3.Height / 8, ClutterBlock.Colors.Red);
                        patch_Level.temporaryEntityData = null;
                        break;
                    case "greenBlocks":
                        ClutterBlockGenerator.Init(self);
                        patch_Level.temporaryEntityData = entity3;
                        ClutterBlockGenerator.Add((int) (entity3.Position.X / 8f), (int) (entity3.Position.Y / 8f), entity3.Width / 8, entity3.Height / 8, ClutterBlock.Colors.Green);
                        patch_Level.temporaryEntityData = null;
                        break;
                    case "oshirodoor":
                        self.Add(new MrOshiroDoor(entity3, vector));
                        break;
                    case "templeMirrorPortal":
                        self.Add(new TempleMirrorPortal(entity3, vector));
                        break;
                    case "reflectionHeartStatue":
                        self.Add(new ReflectionHeartStatue(entity3, vector));
                        break;
                    case "resortRoofEnding":
                        self.Add(new ResortRoofEnding(entity3, vector));
                        break;
                    case "gondola":
                        self.Add(new Gondola(entity3, vector));
                        break;
                    case "birdForsakenCityGem":
                        self.Add(new ForsakenCitySatellite(entity3, vector));
                        break;
                    case "whiteblock":
                        self.Add(new WhiteBlock(entity3, vector));
                        break;
                    case "plateau":
                        self.Add(new Plateau(entity3, vector));
                        break;
                    case "soundSource":
                        self.Add(new SoundSourceEntity(entity3, vector));
                        break;
                    case "templeMirror":
                        self.Add(new TempleMirror(entity3, vector));
                        break;
                    case "templeEye":
                        self.Add(new TempleEye(entity3, vector));
                        break;
                    case "clutterCabinet":
                        self.Add(new ClutterCabinet(entity3, vector));
                        break;
                    case "floatingDebris":
                        self.Add(new FloatingDebris(entity3, vector));
                        break;
                    case "foregroundDebris":
                        self.Add(new ForegroundDebris(entity3, vector));
                        break;
                    case "moonCreature":
                        self.Add(new MoonCreature(entity3, vector));
                        break;
                    case "lightbeam":
                        self.Add(new LightBeam(entity3, vector));
                        break;
                    case "door":
                        self.Add(new Door(entity3, vector));
                        break;
                    case "trapdoor":
                        self.Add(new Trapdoor(entity3, vector));
                        break;
                    case "resortLantern":
                        self.Add(new ResortLantern(entity3, vector));
                        break;
                    case "water":
                        self.Add(new Water(entity3, vector));
                        break;
                    case "waterfall":
                        self.Add(new WaterFall(entity3, vector));
                        break;
                    case "bigWaterfall":
                        self.Add(new BigWaterfall(entity3, vector));
                        break;
                    case "clothesline":
                        self.Add(new Clothesline(entity3, vector));
                        break;
                    case "cliffflag":
                        self.Add(new CliffFlags(entity3, vector));
                        break;
                    case "cliffside_flag":
                        self.Add(new CliffsideWindFlag(entity3, vector));
                        break;
                    case "flutterbird":
                        self.Add(new FlutterBird(entity3, vector));
                        break;
                    case "SoundTest3d":
                        self.Add(new _3dSoundTest(entity3, vector));
                        break;
                    case "SummitBackgroundManager":
                        self.Add(new AscendManager(entity3, vector));
                        break;
                    case "summitGemManager":
                        self.Add(new SummitGemManager(entity3, vector));
                        break;
                    case "heartGemDoor":
                        self.Add(new HeartGemDoor(entity3, vector));
                        break;
                    case "summitcheckpoint":
                        self.Add(new SummitCheckpoint(entity3, vector));
                        break;
                    case "summitcloud":
                        self.Add(new SummitCloud(entity3, vector));
                        break;
                    case "coreMessage":
                        self.Add(new CoreMessage(entity3, vector));
                        break;
                    case "playbackTutorial":
                        self.Add(new PlayerPlayback(entity3, vector));
                        break;
                    case "playbackBillboard":
                        self.Add(new PlaybackBillboard(entity3, vector));
                        break;
                    case "cutsceneNode":
                        self.Add(new CutsceneNode(entity3, vector));
                        break;
                    case "kevins_pc":
                        self.Add(new KevinsPC(entity3, vector));
                        break;
                    case "powerSourceNumber":
                        self.Add(new PowerSourceNumber(entity3.Position + vector, entity3.Int("number", 1), (self as patch_Level).GotCollectables(entity3)));
                        break;
                    case "npc":
                        string text = entity3.Attr("npc").ToLower();
                        Vector2 position = entity3.Position + vector;
                        switch (text) {
                            case "granny_00_house":
                                self.Add(new NPC00_Granny(position));
                                break;
                            case "theo_01_campfire":
                                self.Add(new NPC01_Theo(position));
                                break;
                            case "theo_02_campfire":
                                self.Add(new NPC02_Theo(position));
                                break;
                            case "theo_03_escaping":
                                if (!self.Session.GetFlag("resort_theo")) {
                                    self.Add(new NPC03_Theo_Escaping(position));
                                }
                                break;
                            case "theo_03_vents":
                                self.Add(new NPC03_Theo_Vents(position));
                                break;
                            case "oshiro_03_lobby":
                                self.Add(new NPC03_Oshiro_Lobby(position));
                                break;
                            case "oshiro_03_hallway":
                                self.Add(new NPC03_Oshiro_Hallway1(position));
                                break;
                            case "oshiro_03_hallway2":
                                self.Add(new NPC03_Oshiro_Hallway2(position));
                                break;
                            case "oshiro_03_bigroom":
                                self.Add(new NPC03_Oshiro_Cluttter(entity3, vector));
                                break;
                            case "oshiro_03_breakdown":
                                self.Add(new NPC03_Oshiro_Breakdown(position));
                                break;
                            case "oshiro_03_suite":
                                self.Add(new NPC03_Oshiro_Suite(position));
                                break;
                            case "oshiro_03_rooftop":
                                self.Add(new NPC03_Oshiro_Rooftop(position));
                                break;
                            case "granny_04_cliffside":
                                self.Add(new NPC04_Granny(position));
                                break;
                            case "theo_04_cliffside":
                                self.Add(new NPC04_Theo(position));
                                break;
                            case "theo_05_entrance":
                                self.Add(new NPC05_Theo_Entrance(position));
                                break;
                            case "theo_05_inmirror":
                                self.Add(new NPC05_Theo_Mirror(position));
                                break;
                            case "evil_05":
                                self.Add(new NPC05_Badeline(entity3, vector));
                                break;
                            case "theo_06_plateau":
                                self.Add(new NPC06_Theo_Plateau(entity3, vector));
                                break;
                            case "granny_06_intro":
                                self.Add(new NPC06_Granny(entity3, vector));
                                break;
                            case "badeline_06_crying":
                                self.Add(new NPC06_Badeline_Crying(entity3, vector));
                                break;
                            case "granny_06_ending":
                                self.Add(new NPC06_Granny_Ending(entity3, vector));
                                break;
                            case "theo_06_ending":
                                self.Add(new NPC06_Theo_Ending(entity3, vector));
                                break;
                            case "granny_07x":
                                self.Add(new NPC07X_Granny_Ending(entity3, vector));
                                break;
                            case "theo_08_inside":
                                self.Add(new NPC08_Theo(entity3, vector));
                                break;
                            case "granny_08_inside":
                                self.Add(new NPC08_Granny(entity3, vector));
                                break;
                            case "granny_09_outside":
                                self.Add(new NPC09_Granny_Outside(entity3, vector));
                                break;
                            case "granny_09_inside":
                                self.Add(new NPC09_Granny_Inside(entity3, vector));
                                break;
                            case "gravestone_10":
                                self.Add(new NPC10_Gravestone(entity3, vector));
                                break;
                            case "granny_10_never":
                                self.Add(new NPC07X_Granny_Ending(entity3, vector, ch9EasterEgg: true));
                                break;
                        }
                        break;
                    case "eventTrigger":
                        self.Add(new EventTrigger(entity3, vector));
                        break;
                    case "musicFadeTrigger":
                        self.Add(new MusicFadeTrigger(entity3, vector));
                        break;
                    case "musicTrigger":
                        self.Add(new MusicTrigger(entity3, vector));
                        break;
                    case "altMusicTrigger":
                        self.Add(new AltMusicTrigger(entity3, vector));
                        break;
                    case "cameraOffsetTrigger":
                        self.Add(new CameraOffsetTrigger(entity3, vector));
                        break;
                    case "lightFadeTrigger":
                        self.Add(new LightFadeTrigger(entity3, vector));
                        break;
                    case "bloomFadeTrigger":
                        self.Add(new BloomFadeTrigger(entity3, vector));
                        break;
                    case "cameraTargetTrigger": {
                            string text2 = entity3.Attr("deleteFlag");
                            if (string.IsNullOrEmpty(text2) || !self.Session.GetFlag(text2)) {
                                self.Add(new CameraTargetTrigger(entity3, vector));
                            }
                            break;
                        }
                    case "cameraAdvanceTargetTrigger":
                        self.Add(new CameraAdvanceTargetTrigger(entity3, vector));
                        break;
                    case "respawnTargetTrigger":
                        self.Add(new RespawnTargetTrigger(entity3, vector));
                        break;
                    case "changeRespawnTrigger":
                        self.Add(new ChangeRespawnTrigger(entity3, vector));
                        break;
                    case "windTrigger":
                        self.Add(new WindTrigger(entity3, vector));
                        break;
                    case "windAttackTrigger":
                        self.Add(new WindAttackTrigger(entity3, vector));
                        break;
                    case "minitextboxTrigger":
                        self.Add(new MiniTextboxTrigger(entity3, vector, (entity3 as patch_EntityData).EntityID));
                        break;
                    case "oshiroTrigger":
                        self.Add(new OshiroTrigger(entity3, vector));
                        break;
                    case "interactTrigger":
                        self.Add(new InteractTrigger(entity3, vector));
                        break;
                    case "checkpointBlockerTrigger":
                        self.Add(new CheckpointBlockerTrigger(entity3, vector));
                        break;
                    case "lookoutBlocker":
                        self.Add(new LookoutBlocker(entity3, vector));
                        break;
                    case "stopBoostTrigger":
                        self.Add(new StopBoostTrigger(entity3, vector));
                        break;
                    case "noRefillTrigger":
                        self.Add(new NoRefillTrigger(entity3, vector));
                        break;
                    case "ambienceParamTrigger":
                        self.Add(new AmbienceParamTrigger(entity3, vector));
                        break;
                    case "creditsTrigger":
                        self.Add(new CreditsTrigger(entity3, vector));
                        break;
                    case "goldenBerryCollectTrigger":
                        self.Add(new GoldBerryCollectTrigger(entity3, vector));
                        break;
                    case "moonGlitchBackgroundTrigger":
                        self.Add(new MoonGlitchBackgroundTrigger(entity3, vector));
                        break;
                    case "blackholeStrength":
                        self.Add(new BlackholeStrengthTrigger(entity3, vector));
                        break;
                    case "rumbleTrigger":
                        self.Add(new RumbleTrigger(entity3, vector, (entity3 as patch_EntityData).EntityID));
                        break;
                    case "birdPathTrigger":
                        self.Add(new BirdPathTrigger(entity3, vector));
                        break;
                    case "spawnFacingTrigger":
                        self.Add(new SpawnFacingTrigger(entity3, vector));
                        break;
                    case "detachFollowersTrigger":
                        self.Add(new DetachStrawberryTrigger(entity3, vector));
                        break;
                    default:
                        return false;

                }
                patch_Level.temporaryEntityData = null;
                return true;
            }
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the Godzilla-sized level loading method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLevelLoader))]
    class PatchLevelLoaderAttribute : Attribute { }

    /// <summary>
    /// Patch level loading method to copy decal rotation and color from <see cref="Celeste.DecalData" /> instances into newly created <see cref="Celeste.Decal" /> entities.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLevelLoaderDecalCreation))]
    class PatchLevelLoaderDecalCreationAttribute : Attribute { }

    /// <summary>
    /// Patch the Godzilla-sized level updating method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLevelUpdate))]
    class PatchLevelUpdateAttribute : Attribute { }

    /// <summary>
    /// Patch the Godzilla-sized level rendering method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLevelRender))]
    class PatchLevelRenderAttribute : Attribute { }

    /// <summary>
    /// Patch the Godzilla-sized level transition method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchTransitionRoutine))]
    class PatchTransitionRoutineAttribute : Attribute { }

    /// <summary>
    /// A patch for the CanPause getter that skips the saving check.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLevelCanPause))]
    class PatchLevelCanPauseAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchLevelLoader(ILContext context, CustomAttribute attrib) {
            FieldReference f_Session = context.Method.DeclaringType.FindField("Session");
            FieldReference f_Session_RestartedFromGolden = f_Session.FieldType.Resolve().FindField("RestartedFromGolden");
            MethodDefinition m_cctor = context.Method.DeclaringType.FindMethod(".cctor");
            MethodDefinition m_LoadNewPlayer = context.Method.DeclaringType.FindMethod("Celeste.Player LoadNewPlayerForLevel(Microsoft.Xna.Framework.Vector2,Celeste.PlayerSpriteMode,Celeste.Level)");
            MethodDefinition m_LoadCustomEntity = context.Method.DeclaringType.FindMethod("System.Boolean LoadCustomEntity(Celeste.EntityData,Celeste.Level)");
            MethodDefinition m_PatchHeartGemBehavior = context.Method.DeclaringType.FindMethod("Celeste.AreaMode _PatchHeartGemBehavior(Celeste.AreaMode)");

            // These are used for the static constructor patch
            TypeDefinition t_EntityData = MonoModRule.Modder.Module.GetType("Celeste.EntityData").Resolve();
            FieldDefinition f_EntityData_EntityID = t_EntityData.FindField("EntityID");

            FieldDefinition f_LoadStrings = context.Method.DeclaringType.FindField("_LoadStrings");
            TypeReference t_LoadStrings = f_LoadStrings.FieldType;
            MethodReference m_LoadStrings_Add = MonoModRule.Modder.Module.ImportReference(t_LoadStrings.Resolve().FindMethod("Add"));
            MethodReference m_LoadStrings_ctor = MonoModRule.Modder.Module.ImportReference(t_LoadStrings.Resolve().FindMethod("System.Void .ctor()"));
            m_LoadStrings_Add.DeclaringType = t_LoadStrings;
            m_LoadStrings_ctor.DeclaringType = t_LoadStrings;

            //FieldReference f_isLoadingTriggers = context.Method.DeclaringType.FindField("_isLoadingTriggers")!;
            MethodReference m_IsInDoNotLoadIncreased = context.Method.DeclaringType.FindMethod("_IsInDoNotLoadIncreased")!;

            VariableDefinition[] vars_entityData = context.Body.Variables.Where(v => v.VariableType.FullName == "Celeste.EntityData").ToArray();
            FieldDefinition f_Level_temporaryEntityData = context.Method.DeclaringType.FindField("temporaryEntityData");

            ILCursor cursor = new ILCursor(context);

            // Insert our custom entity loader and use it for levelData.Entities and levelData.Triggers
            //  Before: string name = entityData.Name;
            //  After:  string name = (!Level.LoadCustomEntity(entityData2, this)) ? entityData2.Name : "";
            int nameLoc = -1;
            for (int i = 0; i < 2; i++) {
                cursor.GotoNext(
                    instr => instr.MatchLdfld("Celeste.EntityData", "Name"), // cursor.Next (get entity name)
                    instr => instr.MatchStloc(out nameLoc), // cursor.Next.Next (save entity name)
                    instr => instr.MatchLdloc(out _),
                    instr => instr.MatchCall("<PrivateImplementationDetails>", "System.UInt32 ComputeStringHash(System.String)"));
                cursor.Emit(OpCodes.Dup);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Call, m_LoadCustomEntity);
                cursor.Emit(OpCodes.Brfalse_S, cursor.Next); // False -> custom entity not loaded, so use the vanilla handler
                cursor.Emit(OpCodes.Pop);
                cursor.Emit(OpCodes.Ldstr, "");
                cursor.Emit(OpCodes.Br_S, cursor.Next.Next); // True -> custom entity loaded, so skip the vanilla handler by saving "" as the entity name
                cursor.Index++;
            }


            // Reset to apply EntityID resolving patches - additionally, resolves trigger loading patches
            // Merged with EntityData patch
            cursor.Index = 0;
            // Code change:
            //   foreach (EntityData entity3 in levelData.Entities) {
            // +    Level.temporaryEntityData = entity3;                    This will always be changed at the start of each loop, so i just need to set it to null after the loop.
            //      int iD = entity3.ID;
            //      EntityID entityID = new EntityID(levelData.Name, iD);   Should maybe be removed, but I don't want to break mod parity
            // +    entityID = entity3.EntityID;                            ID offset is managed in LevelData.CreateEntityData
            //      ..., switch(...) { cases ... }
            //   }
            // + Level.temporaryEntityData = null;                          Setting it to null after the loop
            //   ClutterBlockGenerator.Generate();
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdloc(17)); // entity3
            cursor.Emit(OpCodes.Ldloc, 17); // entity3
            cursor.Emit(OpCodes.Stsfld, f_Level_temporaryEntityData);
            cursor.GotoNext(MoveType.Before, instr => instr.MatchLdarg(0));
            cursor.Emit(OpCodes.Ldloc, 17); // entity3
            cursor.Emit(OpCodes.Ldfld, f_EntityData_EntityID); // entity3.EntityID
            cursor.Emit(OpCodes.Stloc, 19); // entityID
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchCall("Celeste.ClutterBlockGenerator", "Generate"));
            cursor.Emit(OpCodes.Ldnull);
            cursor.Emit(OpCodes.Stsfld, f_Level_temporaryEntityData);
            // Code change:
            //   foreach (EntityData trigger in levelData.Triggers) {
            // +    Level.temporaryEntityData = trigger;                            This will always be changed at the start of each loop, so i just need to set it to null after the loop.
            //      int entityID2 = trigger.ID;
            //      EntityID entityID3 = new EntityID(levelData.Name, entityID2);   Should maybe be removed, but I don't want to break mod parity
            // +    entityID = trigger.EntityID;                                    ID offset is managed in LevelData.CreateEntityData
            //      ..., switch(...) { cases ... }
            //   }
            // + Level.temporaryEntityData = null;                                  Setting it to null after the loop, requires HandlerException resolution
            //   foreach (DecalData fgDecal in levelData.FgDecals) ...
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdloc(46)); // trigger
            cursor.Emit(OpCodes.Ldloc, 46); // trigger
            cursor.Emit(OpCodes.Stsfld, f_Level_temporaryEntityData);
            cursor.GotoNext(MoveType.After, instr => instr.MatchLdarg(0));
            cursor.Emit(OpCodes.Ldloc, 46); // trigger
            cursor.Emit(OpCodes.Ldfld, f_EntityData_EntityID); // trigger.EntityID
            cursor.Emit(OpCodes.Stloc, 48); // entityID

            Console.WriteLine("Trigger EntityID hook validated");

            ILLabel continueLabel = null;
            cursor.GotoNext(MoveType.After, instr => instr.MatchBrtrue(out continueLabel));
            // add
            // || _IsInDoNotLoadIncreased(levelData, trigger)
            // to if condition for continue to handle triggers that already add 10000000 to their DoNotLoad entry
            cursor.EmitLdarg0();
            cursor.EmitLdloc(3); // levelData
            cursor.EmitLdloc(46); // trigger
            cursor.EmitCall(m_IsInDoNotLoadIncreased);
            cursor.EmitBrtrue(continueLabel);

            Console.WriteLine("_IsInDoNotLoadIncreased");

            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdloc(out _), instr => instr.MatchLdfld("Celeste.LevelData", "FgDecals"));
            Instruction oldFinallyEnd = cursor.Next;
            // set _isLoadingTriggers to false
            cursor.EmitLdnull();
            Instruction newFinallyEnd = cursor.Prev;
            cursor.EmitStsfld(f_Level_temporaryEntityData);
            // fix end of finally block
            foreach (ExceptionHandler handler in context.Body.ExceptionHandlers.Where(handler => handler.HandlerEnd == oldFinallyEnd)) {
                handler.HandlerEnd = newFinallyEnd;
                break;
            }
            Console.WriteLine("all edits validated");

            // Reset to apply entity patches
            cursor.Index = 0;

            // Patch the winged golden berry so it counts golden deaths as a valid restart
            //  Before: if (this.Session.Dashes == 0 && this.Session.StartedFromBeginning)
            //  After:  if (this.Session.Dashes == 0 && (this.Session.StartedFromBeginning || this.Session.RestartedFromGolden))
            cursor.GotoNext(instr => instr.MatchLdfld("Celeste.Session", "Dashes"));
            cursor.GotoNext(MoveType.After, instr => instr.MatchLdfld("Celeste.Session", "StartedFromBeginning"));
            cursor.Emit(OpCodes.Brtrue_S, cursor.Next.Next); // turn this into an "or" by adding the strawberry immediately if the first condition is true
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_Session);
            cursor.Emit(OpCodes.Ldfld, f_Session_RestartedFromGolden);

            // Patch the HeartGem handler to always load if HeartIsEnd is set in the map meta
            //  Before: if (!this.Session.HeartGem || this.Session.Area.Mode != AreaMode.Normal)
            //  After:  if (!this.Session.HeartGem || this._PatchHeartGemBehavior(this.Session.Area.Mode) != AreaMode.Normal)
            cursor.GotoNext(
                instr => instr.MatchLdfld("Celeste.Level", "Session"),
                instr => instr.MatchLdflda("Celeste.Session", "Area"),
                instr => instr.MatchLdfld("Celeste.AreaKey", "Mode"),
                instr => instr.OpCode == OpCodes.Brfalse);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Index += 3;
            cursor.Emit(OpCodes.Call, m_PatchHeartGemBehavior);

            // Patch Player creation so we avoid ever loading more than one at the same time
            //  Before: Player player = new Player(this.Session.RespawnPoint.Value, spriteMode);
            //  After:  Player player = Level.LoadNewPlayerForLevel(this.Session.RespawnPoint.Value, spriteMode, this);
            cursor.GotoNext(instr => instr.MatchNewobj("Celeste.Player"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Next.OpCode = OpCodes.Call;
            cursor.Next.Operand = m_LoadNewPlayer;

            // Reset to apply static constructor patch
            cursor.Index = 0;

            // Patch the static constructor to populate the _LoadStrings hashset with every vanilla entity name
            // We use _LoadStrings in LoadCustomEntity to determine if an entity name is missing/invalid
            // We manually add "theoCrystalHoldingBarrier" first since its entity handler was removed (unused but still in 5A bin)
            string entityName = "theoCrystalHoldingBarrier";
            new ILContext(m_cctor).Invoke(il => {
                ILCursor cctorCursor = new ILCursor(il);

                cctorCursor.Emit(OpCodes.Newobj, m_LoadStrings_ctor);
                do {
                    cctorCursor.Emit(OpCodes.Dup);
                    cctorCursor.Emit(OpCodes.Ldstr, entityName);
                    cctorCursor.Emit(OpCodes.Callvirt, m_LoadStrings_Add);
                    cctorCursor.Emit(OpCodes.Pop); // HashSet.Add returns a bool.
                }
                while (cursor.TryGotoNext(
                    instr => instr.MatchLdloc(nameLoc), // We located this in our entity loader patch
                    instr => instr.MatchLdstr(out entityName))
                );
                cctorCursor.Emit(OpCodes.Stsfld, f_LoadStrings);
            });
        }

        public static void PatchLevelLoaderDecalCreation(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_DecalData = MonoModRule.Modder.FindType("Celeste.DecalData").Resolve();
            TypeDefinition t_Decal = MonoModRule.Modder.FindType("Celeste.Decal").Resolve();

            FieldDefinition f_DecalData_Rotation = t_DecalData.FindField("Rotation");
            FieldDefinition f_DecalData_ColorHex = t_DecalData.FindField("ColorHex");

            MethodDefinition m_Decal_ctor = t_Decal.FindMethod("System.Void .ctor(System.String,Microsoft.Xna.Framework.Vector2,Microsoft.Xna.Framework.Vector2,System.Int32,System.Single,System.String)");

            ILCursor cursor = new ILCursor(context);

            int loc_decaldata = -1;
            int matches = 0;
            // move to just before each of the two Decal constructor calls (one for FGDecals and one for BGDecals), and obtain a reference to the DecalData local
            while (cursor.TryGotoNext(MoveType.After,
                                      instr => instr.MatchLdloc(out loc_decaldata),
                                      instr => instr.MatchLdfld("Celeste.DecalData", "Scale"),
                                      instr => instr.MatchLdcI4(Celeste.Depths.FGDecals)
                                            || instr.MatchLdcI4(Celeste.Depths.BGDecals))) {
                // load the rotation from the DecalData
                cursor.Emit(OpCodes.Ldloc_S, (byte) loc_decaldata);
                cursor.Emit(OpCodes.Ldfld, f_DecalData_Rotation);
                cursor.Emit(OpCodes.Ldloc_S, (byte) loc_decaldata);
                cursor.Emit(OpCodes.Ldfld, f_DecalData_ColorHex);
                // and replace the Decal constructor to accept it
                cursor.Emit(OpCodes.Newobj, m_Decal_ctor);
                cursor.Remove();

                matches++;
            }
            if (matches != 2) {
                throw new Exception($"Too few matches for HasAttr(\"tag\"): {matches}");
            }
        }

        public static void PatchLevelUpdate(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_FixChaserStatesTimeStamp = context.Method.DeclaringType.FindMethod("FixChaserStatesTimeStamp");
            MethodDefinition m_CheckForErrors = context.Method.DeclaringType.FindMethod("CheckForErrors");
            MethodReference m_Everest_CoreModule_Settings = MonoModRule.Modder.Module.GetType("Celeste.Mod.Core.CoreModule").FindProperty("Settings").GetMethod;
            TypeDefinition t_Everest_CoreModuleSettings = MonoModRule.Modder.Module.GetType("Celeste.Mod.Core.CoreModuleSettings");
            MethodReference m_ButtonBinding_Pressed = MonoModRule.Modder.Module.GetType("Celeste.Mod.ButtonBinding").FindProperty("Pressed").GetMethod;

            ILCursor cursor = new ILCursor(context);

            // Insert CheckForErrors() at the beginning so we can display an error screen if needed
            cursor.Emit(OpCodes.Ldarg_0).Emit(OpCodes.Call, m_CheckForErrors);
            // Insert an if statement that returns if we find an error at CheckForErrors
            ILLabel rest = cursor.DefineLabel();
            cursor.Emit(OpCodes.Brfalse, rest).Emit(OpCodes.Ret);

            // insert FixChaserStatesTimeStamp()
            cursor.MarkLabel(rest);
            cursor.MoveAfterLabels();
            cursor.Emit(OpCodes.Ldarg_0).Emit(OpCodes.Call, m_FixChaserStatesTimeStamp);

            /* We expect something similar enough to the following:
            call class Monocle.MInput/KeyboardData Monocle.MInput::get_Keyboard() // We're here
            ldc.i4.s 9
            callvirt instance bool Monocle.MInput/KeyboardData::Pressed(valuetype [FNA]Microsoft.Xna.Framework.Input.Keys)

            We're replacing
            MInput.Keyboard.Pressed(Keys.Tab)
            with
            CoreModule.Settings.DebugMap.Pressed
            */

            cursor.GotoNext(instr => instr.MatchCall("Monocle.MInput", "get_Keyboard"),
                instr => instr.GetIntOrNull() == 9,
                instr => instr.MatchCallvirt("Monocle.MInput/KeyboardData", "Pressed"));
            // Remove the offending instructions, and replace them with property getter
            cursor.RemoveRange(3);
            cursor.Emit(OpCodes.Call, m_Everest_CoreModule_Settings);
            cursor.Emit(OpCodes.Call, t_Everest_CoreModuleSettings.FindProperty("DebugMap").GetMethod);
            cursor.Emit(OpCodes.Call, m_ButtonBinding_Pressed);
        }

        public static void PatchLevelRender(ILContext context, CustomAttribute attrib) {
            FieldDefinition f_SubHudRenderer = context.Method.DeclaringType.FindField("SubHudRenderer");

            /* We expect something similar enough to the following:
            if (!this.Paused || !this.PauseMainMenuOpen || !Input.MenuJournal.Check || !this.AllowHudHide)
            {
                this.HudRenderer.Render(this);
            }
            and we want to prepend it with:
            this.SubHudRenderer.Render(this);
            */

            ILCursor cursor = new ILCursor(context);
            // Make use of the pre-existing Ldarg_0
            cursor.GotoNext(instr => instr.MatchLdfld("Monocle.Scene", "Paused"));
            // Retrieve a reference to Renderer.Render(Scene) from the following this.HudRenderer.Render(this)
            cursor.FindNext(out ILCursor[] render, instr => instr.MatchCallvirt("Monocle.Renderer", "Render"));
            MethodReference m_Renderer_Render = (MethodReference) render[0].Next.Operand;

            cursor.Emit(OpCodes.Ldfld, f_SubHudRenderer);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Callvirt, m_Renderer_Render);
            // Re-add the Ldarg_0 we cannibalized
            cursor.Emit(OpCodes.Ldarg_0);
        }

        public static void PatchTransitionRoutine(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_GCCollect = method.DeclaringType.FindMethod("System.Void _GCCollect()");

            // The level transition routine is stored in a compiler-generated method.
            method = method.GetEnumeratorMoveNext();

            new ILContext(method).Invoke(il => {
                ILCursor cursor = new ILCursor(il);
                cursor.GotoNext(instr => instr.MatchCall("System.GC", "Collect"));
                // Replace the method call.
                cursor.Next.Operand = m_GCCollect;
            });
        }

        public static void PatchLevelCanPause(ILContext il, CustomAttribute attrib) {
            ILCursor c = new ILCursor(il);
            c.GotoNext(MoveType.After, instr => instr.MatchCall("Celeste.UserIO", "get_Saving"));
            c.Emit(OpCodes.Pop);
            c.Emit(OpCodes.Ldc_I4_0);
        }

    }
}
