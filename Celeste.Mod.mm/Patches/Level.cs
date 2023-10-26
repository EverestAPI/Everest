#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Entities;
using Celeste.Mod.Meta;
using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.InlineRT;
using Celeste.Mod.Helpers;

namespace Celeste {
    class patch_Level : Level {

        // This is used within Level.LoadEntity and Level.orig_LoadLevel so that entityData can be passed to ClutterBlocks being added to the scene.
        internal static EntityData temporaryEntityData = null;

        // We're effectively in GameLoader, but still need to "expose" private fields to our mod.
        private static EventInstance PauseSnapshot;
        public static EventInstance _PauseSnapshot => PauseSnapshot;

        private static HashSet<string> _LoadStrings; // Generated in MonoModRules.PatchLevelLoader

        public SubHudRenderer SubHudRenderer;
        public static Player NextLoadedPlayer;
        public static int SkipScreenWipes;
        public static bool ShouldAutoPause = false;

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
            if (onComplete == null && !hiresSnow && SkipScreenWipes > 0) {
                SkipScreenWipes--;
                return;
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

            if (Entities.GetToAdd().FirstOrDefault(e => e is TextMenu) is TextMenu menu) {
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
                && AreaData.GetMode(Session.Area)?.GetMapMeta() is MapMeta mapMeta && (mapMeta.OverrideASideMeta ?? false)
                && mapMeta.IntroType is Player.IntroTypes introType)
                playerIntro = introType;

            try {
                Logger.Log(LogLevel.Verbose, "LoadLevel", $"Loading room {Session.LevelData.Name} of {Session.Area.GetSID()}");

                orig_LoadLevel(playerIntro, isFromLoader);

                if (ShouldAutoPause) {
                    ShouldAutoPause = false;
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

        private AreaMode _PatchHeartGemBehavior(AreaMode levelMode) {
            if (Session.Area.GetLevelSet() == "Celeste") {
                // do not mess with vanilla.
                return levelMode;
            }

            MapMetaModeProperties properties = Session.MapData.GetMeta();
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

        // Called from LoadLevel, patched via MonoModRules.PatchLevelLoader
        private static Player LoadNewPlayer(Vector2 position, PlayerSpriteMode spriteMode) {
            Player player = NextLoadedPlayer;
            if (player != null) {
                NextLoadedPlayer = null;
                return player;
            }

            return new Player(position, spriteMode);
        }

        // Called from LoadLevel, patched via MonoModRules.PatchLevelLoader
        public static Entity RegisterEntityDataWithEntity(Entity e, EntityData d) {
            (e as patch_Entity).__EntityData = d;
            return e;
        }

        /// <summary>
        /// Search for a custom entity that matches the <see cref="EntityData.Name"/>.<br/>
        /// To register a custom entity, use <see cref="CustomEntityAttribute"/> or <see cref="Everest.Events.Level.OnLoadEntity"/>.<br/>
        /// <seealso href="https://github.com/EverestAPI/Resources/wiki/Custom-Entities-and-Triggers">Read More</seealso>.
        /// </summary>
        /// <param name="entityData"></param>
        /// <param name="level">The level to add the entity to.</param>
        /// <returns></returns>
        public static bool LoadCustomEntity(EntityData entityData, Level level) {
            LevelData levelData = level.Session.LevelData;
            Vector2 offset = new Vector2(levelData.Bounds.Left, levelData.Bounds.Top);
            // Theoretically possible to solve some cases of old helper EntityData referencing
            if (Everest.Events.Level.LoadEntity(level, levelData, offset, entityData))
                return true;
            Entity loaded;
            if (EntityLoaders.TryGetValue(entityData.Name, out EntityLoader loader)) {
                loaded = loader(level, levelData, offset, entityData);
                if (loaded != null) {
                    (loaded as patch_Entity).__EntityData = entityData;
                    level.Add(loaded);
                    return true;
                }
            }

            if (entityData.Name == "everest/spaceController") {
                loaded = new SpaceController();
                (loaded as patch_Entity).__EntityData = entityData;
                level.Add(loaded);
                return true;
            }

            // The following entities have hardcoded "attributes."
            // Everest allows custom maps to set them.

            if (entityData.Name == "spinner") {
                if (level.Session.Area.ID == 3 ||
                    (level.Session.Area.ID == 7 && level.Session.Level.StartsWith("d-")) ||
                    entityData.Bool("dust")) {
                    loaded = new DustStaticSpinner(entityData, offset);
                    (loaded as patch_Entity).__EntityData = entityData;
                    level.Add(loaded);
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

                loaded = new CrystalStaticSpinner(entityData, offset, color);
                (loaded as patch_Entity).__EntityData = entityData;
                level.Add(loaded);
                return true;
            }

            if (entityData.Name == "trackSpinner") {
                if (level.Session.Area.ID == 10 ||
                    entityData.Bool("star")) {
                    loaded = new StarTrackSpinner(entityData, offset);
                } else if (level.Session.Area.ID == 3 ||
                    (level.Session.Area.ID == 7 && level.Session.Level.StartsWith("d-")) ||
                    entityData.Bool("dust")) {
                    loaded = new DustTrackSpinner(entityData, offset);
                } else {
                    loaded = new BladeTrackSpinner(entityData, offset);
                }

                (loaded as patch_Entity).__EntityData = entityData;
                level.Add(loaded);
                return true;
            }

            if (entityData.Name == "rotateSpinner") {
                if (level.Session.Area.ID == 10 ||
                    entityData.Bool("star")) {
                    loaded = new StarRotateSpinner(entityData, offset);
                } else if (level.Session.Area.ID == 3 ||
                    (level.Session.Area.ID == 7 && level.Session.Level.StartsWith("d-")) ||
                    entityData.Bool("dust")) {
                    loaded = new DustRotateSpinner(entityData, offset);
                } else {
                    loaded = new BladeRotateSpinner(entityData, offset);
                }
                (loaded as patch_Entity).__EntityData = entityData;
                level.Add(loaded);
                return true;
            }

            if (entityData.Name == "checkpoint" &&
                entityData.Position == Vector2.Zero &&
                !entityData.Bool("allowOrigin")) {
                // Workaround for mod levels with old versions of Ahorn containing a checkpoint at (0, 0):
                // Create the checkpoint and avoid the start position update in orig_Load.
                loaded = new Checkpoint(entityData, offset);
                (loaded as patch_Entity).__EntityData = entityData;
                level.Add(loaded);
                return true;
            }

            if (entityData.Name == "cloud") {
                patch_Cloud cloud = new Cloud(entityData, offset) as patch_Cloud;
                if (entityData.Has("small"))
                    cloud.Small = entityData.Bool("small");
                ((Entity)cloud as patch_Entity).__EntityData = entityData;
                level.Add(cloud);
                return true;
            }

            if (entityData.Name == "cobweb") {
                patch_Cobweb cobweb = new Cobweb(entityData, offset) as patch_Cobweb;
                if (entityData.Has("color"))
                    cobweb.OverrideColors = entityData.Attr("color")?.Split(',').Select(s => Calc.HexToColor(s)).ToArray();
                ((Entity) cobweb as patch_Entity).__EntityData = entityData;
                level.Add(cobweb);
                return true;
            }

            if (entityData.Name == "movingPlatform") {
                patch_MovingPlatform platform = new MovingPlatform(entityData, offset) as patch_MovingPlatform;
                if (entityData.Has("texture"))
                    platform.OverrideTexture = entityData.Attr("texture");
                ((Entity)platform as patch_Entity).__EntityData = entityData;
                level.Add(platform);
                return true;
            }

            if (entityData.Name == "sinkingPlatform") {
                patch_SinkingPlatform platform = new SinkingPlatform(entityData, offset) as patch_SinkingPlatform;
                if (entityData.Has("texture"))
                    platform.OverrideTexture = entityData.Attr("texture");
                ((Entity)platform as patch_Entity).__EntityData = entityData;
                level.Add(platform);
                return true;
            }

            if (entityData.Name == "crumbleBlock") {
                patch_CrumblePlatform platform = new CrumblePlatform(entityData, offset) as patch_CrumblePlatform;
                if (entityData.Has("texture"))
                    platform.OverrideTexture = entityData.Attr("texture");
                ((Entity) platform as patch_Entity).__EntityData = entityData;
                level.Add(platform);
                return true;
            }

            if (entityData.Name == "wire") {
                Wire wire = new Wire(entityData, offset);
                if (entityData.Has("color"))
                    wire.Color = entityData.HexColor("color");
                ((Entity)wire as patch_Entity).__EntityData = entityData;
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

        [MonoModIgnore]
        private extern bool GotCollectables(EntityData data);

       
        public bool LoadEntity(EntityData entity3, bool useCurrentRoom = true) {
            LevelData levelData = useCurrentRoom ? Session.LevelData : (entity3.Level ?? Session.LevelData);
            Vector2 vector = levelData.Position;
            int iD = entity3.ID;
            EntityID entityID = new EntityID(levelData.Name, iD);
            if (Session.DoNotLoad.Contains(entityID)) {
                return false;
            } else if(LoadCustomEntity(entity3, this)) {
                return true;
            } else {
                Entity e = null;
                switch (entity3.Name) {
                    case "jumpThru":
                        Add(e = new JumpthruPlatform(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "refill":
                        Add(e = new Refill(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "infiniteStar":
                        Add(e = new FlyFeather(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "strawberry":
                        Add(e = new Strawberry(entity3, vector, entityID));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "memorialTextController":
                        if (Session.Dashes == 0 && (Session.StartedFromBeginning || (this.Session as patch_Session).RestartedFromGolden)) {
                            Add(e = new Strawberry(entity3, vector, entityID));
                            (e as patch_Entity).__EntityData = entity3;
                        }
                        break;
                    case "goldenBerry": {
                            bool cheatMode = SaveData.Instance.CheatMode;
                            bool flag4 = Session.FurthestSeenLevel == Session.Level || Session.Deaths == 0;
                            bool flag5 = SaveData.Instance.UnlockedModes >= 3 || SaveData.Instance.DebugMode;
                            bool completed = (SaveData.Instance as patch_SaveData).Areas_Safe[Session.Area.ID].Modes[(int) Session.Area.Mode].Completed;
                            if ((cheatMode || (flag5 && completed)) && flag4) {
                                Add(e = new Strawberry(entity3, vector, entityID));
                                (e as patch_Entity).__EntityData = entity3;
                            }
                            break;
                        }
                    case "summitgem":
                        Add(e = new SummitGem(entity3, vector, entityID));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "blackGem":
                        if (!Session.HeartGem || _PatchHeartGemBehavior(Session.Area.Mode) != 0) {
                            Add(e = new HeartGem(entity3, vector));
                            (e as patch_Entity).__EntityData = entity3;
                        }
                        break;
                    case "dreamHeartGem":
                        if (!Session.HeartGem) {
                            Add(e = new DreamHeartGem(entity3, vector));
                            (e as patch_Entity).__EntityData = entity3;
                        }
                        break;
                    case "spring":
                        Add(e = new Spring(entity3, vector, Spring.Orientations.Floor));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "wallSpringLeft":
                        Add(e = new Spring(entity3, vector, Spring.Orientations.WallLeft));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "wallSpringRight":
                        Add(e = new Spring(entity3, vector, Spring.Orientations.WallRight));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "fallingBlock":
                        Add(e = new FallingBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "zipMover":
                        Add(e = new ZipMover(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "crumbleBlock":
                        Add(e = new CrumblePlatform(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "dreamBlock":
                        Add(e = new DreamBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "touchSwitch":
                        Add(e = new TouchSwitch(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "switchGate":
                        Add(e = new SwitchGate(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "negaBlock":
                        Add(e = new NegaBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "key":
                        Add(e = new Key(entity3, vector, entityID));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "lockBlock":
                        Add(e = new LockBlock(entity3, vector, entityID));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "movingPlatform":
                        Add(e = new MovingPlatform(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
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
                                Add(e = new RotatingPlatform(position2, width, vector3, clockwise));
                                (e as patch_Entity).__EntityData = entity3;
                            }
                            break;
                        }
                    case "blockField":
                        Add(e = new BlockField(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "cloud":
                        Add(e = new Cloud(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "booster":
                        Add(e = new Booster(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "moveBlock":
                        Add(e = new MoveBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "light":
                        Add(e = new PropLight(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "switchBlock":
                    case "swapBlock":
                        Add(e = new SwapBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "dashSwitchH":
                    case "dashSwitchV":
                        Add(e = DashSwitch.Create(entity3, vector, entityID));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "templeGate":
                        Add(e = new TempleGate(entity3, vector, levelData.Name));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "torch":
                        Add(e = new Torch(entity3, vector, entityID));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "templeCrackedBlock":
                        Add(e = new TempleCrackedBlock(entityID, entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "seekerBarrier":
                        Add(e = new SeekerBarrier(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "theoCrystal":
                        Add(e = new TheoCrystal(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "glider":
                        Add(e = new Glider(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "theoCrystalPedestal":
                        Add(e = new TheoCrystalPedestal(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "badelineBoost":
                        Add(e = new BadelineBoost(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "cassette":
                        if (!Session.Cassette) {
                            Add(e = new Cassette(entity3, vector));
                            (e as patch_Entity).__EntityData = entity3;
                        }
                        break;
                    case "cassetteBlock": {
                            CassetteBlock cassetteBlock = new CassetteBlock(entity3, vector, entityID);
                            Add(e = cassetteBlock);
                            (e as patch_Entity).__EntityData = entity3;
                            HasCassetteBlocks = true;
                            if (CassetteBlockTempo == 1f) {
                                CassetteBlockTempo = cassetteBlock.Tempo;
                            }
                            CassetteBlockBeats = Math.Max(cassetteBlock.Index + 1, CassetteBlockBeats);
                            if (base.Tracker.GetEntity<CassetteBlockManager>() == null && (Session.Area.Mode != AreaMode.Normal || !Session.Cassette)) {
                                Add(new CassetteBlockManager());
                            }
                            break;
                        }
                    case "wallBooster":
                        Add(e = new WallBooster(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "bounceBlock":
                        Add(e = new BounceBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "coreModeToggle":
                        Add(e = new CoreModeToggle(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "iceBlock":
                        Add(e = new IceBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "fireBarrier":
                        Add(e = new FireBarrier(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "eyebomb":
                        Add(e = new Puffer(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "flingBird":
                        Add(e = new FlingBird(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "flingBirdIntro":
                        Add(e = new FlingBirdIntro(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "birdPath":
                        Add(e = new BirdPath(entityID, entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "lightningBlock":
                        Add(e = new LightningBreakerBox(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "spikesUp":
                        Add(e = new Spikes(entity3, vector, Spikes.Directions.Up));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "spikesDown":
                        Add(e = new Spikes(entity3, vector, Spikes.Directions.Down));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "spikesLeft":
                        Add(e = new Spikes(entity3, vector, Spikes.Directions.Left));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "spikesRight":
                        Add(e = new Spikes(entity3, vector, Spikes.Directions.Right));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "triggerSpikesUp":
                        Add(e = new TriggerSpikes(entity3, vector, TriggerSpikes.Directions.Up));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "triggerSpikesDown":
                        Add(e = new TriggerSpikes(entity3, vector, TriggerSpikes.Directions.Down));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "triggerSpikesRight":
                        Add(e = new TriggerSpikes(entity3, vector, TriggerSpikes.Directions.Right));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "triggerSpikesLeft":
                        Add(e = new TriggerSpikes(entity3, vector, TriggerSpikes.Directions.Left));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "darkChaser":
                        Add(e = new BadelineOldsite(entity3, vector, Tracker.CountEntities<BadelineOldsite>()));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "rotateSpinner":
                        if (Session.Area.ID == 10) {
                            Add(e = new StarRotateSpinner(entity3, vector));
                        } else if (Session.Area.ID == 3 || (Session.Area.ID == 7 && Session.Level.StartsWith("d-"))) {
                            Add(e = new DustRotateSpinner(entity3, vector));
                        } else {
                            Add(e = new BladeRotateSpinner(entity3, vector));
                        }
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "trackSpinner":
                        if (Session.Area.ID == 10) {
                            Add(e = new StarTrackSpinner(entity3, vector));
                        } else if (Session.Area.ID == 3 || (Session.Area.ID == 7 && Session.Level.StartsWith("d-"))) {
                            Add(e = new DustTrackSpinner(entity3, vector));
                        } else {
                            Add(e = new BladeTrackSpinner(entity3, vector));
                        }
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "spinner": {
                            if (Session.Area.ID == 3 || (Session.Area.ID == 7 && Session.Level.StartsWith("d-"))) {
                                Add(e = new DustStaticSpinner(entity3, vector));
                                (e as patch_Entity).__EntityData = entity3;
                                break;
                            }
                            CrystalColor color = CrystalColor.Blue;
                            if (Session.Area.ID == 5) {
                                color = CrystalColor.Red;
                            } else if (Session.Area.ID == 6) {
                                color = CrystalColor.Purple;
                            } else if (Session.Area.ID == 10) {
                                color = CrystalColor.Rainbow;
                            }
                            Add(e = new CrystalStaticSpinner(entity3, vector, color));
                            (e as patch_Entity).__EntityData = entity3;
                            break;
                        }
                    case "sinkingPlatform":
                        Add(e = new SinkingPlatform(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "friendlyGhost":
                        Add(e = new AngryOshiro(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "seeker":
                        Add(e = new Seeker(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "seekerStatue":
                        Add(e = new SeekerStatue(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "slider":
                        Add(e = new Slider(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "templeBigEyeball":
                        Add(e = new TempleBigEyeball(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "crushBlock":
                        Add(e = new CrushBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "bigSpinner":
                        Add(e = new Bumper(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "starJumpBlock":
                        Add(e = new StarJumpBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "floatySpaceBlock":
                        Add(e = new FloatySpaceBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "glassBlock":
                        Add(e = new GlassBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "goldenBlock":
                        Add(e = new GoldenBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "fireBall":
                        Add(e = new FireBall(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "risingLava":
                        Add(e = new RisingLava(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "sandwichLava":
                        Add(e = new SandwichLava(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "killbox":
                        Add(e = new Killbox(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "fakeHeart":
                        Add(e = new FakeHeart(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "lightning":
                        if (entity3.Bool("perLevel") || !Session.GetFlag("disable_lightning")) {
                            Add(e = new Lightning(entity3, vector));
                            (e as patch_Entity).__EntityData = entity3;
                        }
                        break;
                    case "finalBoss":
                        Add(e = new FinalBoss(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "finalBossFallingBlock":
                        Add(e = FallingBlock.CreateFinalBossBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "finalBossMovingBlock":
                        Add(e = new FinalBossMovingBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "fakeWall":
                        Add(e = new FakeWall(entityID, entity3, vector, FakeWall.Modes.Wall));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "fakeBlock":
                        Add(e = new FakeWall(entityID, entity3, vector, FakeWall.Modes.Block));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "dashBlock":
                        Add(e = new DashBlock(entity3, vector, entityID));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "invisibleBarrier":
                        Add(e = new InvisibleBarrier(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "exitBlock":
                        Add(e = new ExitBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "conditionBlock": {
                            string conditionBlockModes = entity3.Attr("condition", "Key");
                            EntityID none = EntityID.None;
                            string[] array = entity3.Attr("conditionID").Split(':');
                            none.Level = array[0];
                            none.ID = Convert.ToInt32(array[1]);
                            if (conditionBlockModes.ToLowerInvariant() switch {
                                "button" => Session.GetFlag(DashSwitch.GetFlagName(none)),
                                "key" => Session.DoNotLoad.Contains(none),
                                "strawberry" => Session.Strawberries.Contains(none),
                                _ => throw new Exception("Condition type not supported!"),
                            }) {
                                Add(e = new ExitBlock(entity3, vector));
                                (e as patch_Entity).__EntityData = entity3;
                            }
                            break;
                        }
                    case "coverupWall":
                        Add(e = new CoverupWall(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "crumbleWallOnRumble":
                        Add(e = new CrumbleWallOnRumble(entity3, vector, entityID));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "ridgeGate":
                        if (GotCollectables(entity3)) {
                            Add(e = new RidgeGate(entity3, vector));
                            (e as patch_Entity).__EntityData = entity3;
                        }
                        break;
                    case "tentacles":
                        Add(e = new ReflectionTentacles(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "starClimbController":
                        Add(e = new StarJumpController());
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "playerSeeker":
                        Add(e = new PlayerSeeker(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "chaserBarrier":
                        Add(e = new ChaserBarrier(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "introCrusher":
                        Add(e = new IntroCrusher(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "bridge":
                        Add(e = new Bridge(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "bridgeFixed":
                        Add(e = new BridgeFixed(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "bird":
                        Add(e = new BirdNPC(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "introCar":
                        Add(e = new IntroCar(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "memorial":
                        Add(e = new Memorial(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "wire":
                        Add(e = new Wire(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "cobweb":
                        Add(e = new Cobweb(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "lamp":
                        Add(e = new Lamp(vector + entity3.Position, entity3.Bool("broken")));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "hanginglamp":
                        Add(e = new HangingLamp(entity3, vector + entity3.Position));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "hahaha":
                        Add(e = new Hahaha(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "bonfire":
                        Add(e = new Bonfire(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "payphone":
                        Add(e = new Payphone(vector + entity3.Position));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "colorSwitch":
                        Add(e = new ClutterSwitch(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "clutterDoor":
                        Add(e = new ClutterDoor(entity3, vector, Session));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "dreammirror":
                        Add(e = new DreamMirror(vector + entity3.Position));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "resortmirror":
                        Add(e = new ResortMirror(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "towerviewer":
                        Add(e = new Lookout(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "picoconsole":
                        Add(e = new PicoConsole(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "wavedashmachine":
                        Add(e = new WaveDashTutorialMachine(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "yellowBlocks":
                        ClutterBlockGenerator.Init(this);
                        temporaryEntityData = entity3;
                        ClutterBlockGenerator.Add((int) (entity3.Position.X / 8f), (int) (entity3.Position.Y / 8f), entity3.Width / 8, entity3.Height / 8, ClutterBlock.Colors.Yellow);
                        patch_Level.temporaryEntityData = null;
                        break;
                    case "redBlocks":
                        ClutterBlockGenerator.Init(this);
                        temporaryEntityData = entity3;
                        ClutterBlockGenerator.Add((int) (entity3.Position.X / 8f), (int) (entity3.Position.Y / 8f), entity3.Width / 8, entity3.Height / 8, ClutterBlock.Colors.Red);
                        temporaryEntityData = null;
                        break;
                    case "greenBlocks":
                        ClutterBlockGenerator.Init(this);
                        temporaryEntityData = entity3;
                        ClutterBlockGenerator.Add((int) (entity3.Position.X / 8f), (int) (entity3.Position.Y / 8f), entity3.Width / 8, entity3.Height / 8, ClutterBlock.Colors.Green);
                        temporaryEntityData = null;
                        break;
                    case "oshirodoor":
                        Add(e = new MrOshiroDoor(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "templeMirrorPortal":
                        Add(e = new TempleMirrorPortal(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "reflectionHeartStatue":
                        Add(e = new ReflectionHeartStatue(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "resortRoofEnding":
                        Add(e = new ResortRoofEnding(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "gondola":
                        Add(e = new Gondola(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "birdForsakenCityGem":
                        Add(e = new ForsakenCitySatellite(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "whiteblock":
                        Add(e = new WhiteBlock(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "plateau":
                        Add(e = new Plateau(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "soundSource":
                        Add(e = new SoundSourceEntity(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "templeMirror":
                        Add(e = new TempleMirror(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "templeEye":
                        Add(e = new TempleEye(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "clutterCabinet":
                        Add(e = new ClutterCabinet(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "floatingDebris":
                        Add(e = new FloatingDebris(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "foregroundDebris":
                        Add(e = new ForegroundDebris(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "moonCreature":
                        Add(e = new MoonCreature(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "lightbeam":
                        Add(e = new LightBeam(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "door":
                        Add(e = new Door(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "trapdoor":
                        Add(e = new Trapdoor(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "resortLantern":
                        Add(e = new ResortLantern(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "water":
                        Add(e = new Water(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "waterfall":
                        Add(e = new WaterFall(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "bigWaterfall":
                        Add(e = new BigWaterfall(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "clothesline":
                        Add(e = new Clothesline(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "cliffflag":
                        Add(e = new CliffFlags(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "cliffside_flag":
                        Add(e = new CliffsideWindFlag(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "flutterbird":
                        Add(e = new FlutterBird(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "SoundTest3d":
                        Add(e = new _3dSoundTest(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "SummitBackgroundManager":
                        Add(e = new AscendManager(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "summitGemManager":
                        Add(e = new SummitGemManager(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "heartGemDoor":
                        Add(e = new HeartGemDoor(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "summitcheckpoint":
                        Add(e = new SummitCheckpoint(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "summitcloud":
                        Add(e = new SummitCloud(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "coreMessage":
                        Add(e = new CoreMessage(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "playbackTutorial":
                        Add(e = new PlayerPlayback(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "playbackBillboard":
                        Add(e = new PlaybackBillboard(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "cutsceneNode":
                        Add(e = new CutsceneNode(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "kevins_pc":
                        Add(e = new KevinsPC(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "powerSourceNumber":
                        Add(e = new PowerSourceNumber(entity3.Position + vector, entity3.Int("number", 1), GotCollectables(entity3)));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "npc":
                        string text = entity3.Attr("npc").ToLower();
                        Vector2 position = entity3.Position + vector;
                        switch (text) {
                            case "granny_00_house":
                                Add(e = new NPC00_Granny(position));
                                break;
                            case "theo_01_campfire":
                                Add(e = new NPC01_Theo(position));
                                break;
                            case "theo_02_campfire":
                                Add(e = new NPC02_Theo(position));
                                break;
                            case "theo_03_escaping":
                                if (!Session.GetFlag("resort_theo")) {
                                    Add(e = new NPC03_Theo_Escaping(position));
                                }
                                break;
                            case "theo_03_vents":
                                Add(e = new NPC03_Theo_Vents(position));
                                break;
                            case "oshiro_03_lobby":
                                Add(e = new NPC03_Oshiro_Lobby(position));
                                break;
                            case "oshiro_03_hallway":
                                Add(e = new NPC03_Oshiro_Hallway1(position));
                                break;
                            case "oshiro_03_hallway2":
                                Add(e = new NPC03_Oshiro_Hallway2(position));
                                break;
                            case "oshiro_03_bigroom":
                                Add(e = new NPC03_Oshiro_Cluttter(entity3, vector));
                                break;
                            case "oshiro_03_breakdown":
                                Add(e = new NPC03_Oshiro_Breakdown(position));
                                break;
                            case "oshiro_03_suite":
                                Add(e = new NPC03_Oshiro_Suite(position));
                                break;
                            case "oshiro_03_rooftop":
                                Add(e = new NPC03_Oshiro_Rooftop(position));
                                break;
                            case "granny_04_cliffside":
                                Add(e = new NPC04_Granny(position));
                                break;
                            case "theo_04_cliffside":
                                Add(e = new NPC04_Theo(position));
                                break;
                            case "theo_05_entrance":
                                Add(e = new NPC05_Theo_Entrance(position));
                                break;
                            case "theo_05_inmirror":
                                Add(e = new NPC05_Theo_Mirror(position));
                                break;
                            case "evil_05":
                                Add(e = new NPC05_Badeline(entity3, vector));
                                break;
                            case "theo_06_plateau":
                                Add(e = new NPC06_Theo_Plateau(entity3, vector));
                                break;
                            case "granny_06_intro":
                                Add(e = new NPC06_Granny(entity3, vector));
                                break;
                            case "badeline_06_crying":
                                Add(e = new NPC06_Badeline_Crying(entity3, vector));
                                break;
                            case "granny_06_ending":
                                Add(e = new NPC06_Granny_Ending(entity3, vector));
                                break;
                            case "theo_06_ending":
                                Add(e = new NPC06_Theo_Ending(entity3, vector));
                                break;
                            case "granny_07x":
                                Add(e = new NPC07X_Granny_Ending(entity3, vector));
                                break;
                            case "theo_08_inside":
                                Add(e = new NPC08_Theo(entity3, vector));
                                break;
                            case "granny_08_inside":
                                Add(e = new NPC08_Granny(entity3, vector));
                                break;
                            case "granny_09_outside":
                                Add(e = new NPC09_Granny_Outside(entity3, vector));
                                break;
                            case "granny_09_inside":
                                Add(e = new NPC09_Granny_Inside(entity3, vector));
                                break;
                            case "gravestone_10":
                                Add(e = new NPC10_Gravestone(entity3, vector));
                                break;
                            case "granny_10_never":
                                Add(e = new NPC07X_Granny_Ending(entity3, vector, ch9EasterEgg: true));
                                break;
                        }
                        if (e != null)
                            (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "eventTrigger":
                        Add(e = new EventTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "musicFadeTrigger":
                        Add(e = new MusicFadeTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "musicTrigger":
                        Add(e = new MusicTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "altMusicTrigger":
                        Add(e = new AltMusicTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "cameraOffsetTrigger":
                        Add(e = new CameraOffsetTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "lightFadeTrigger":
                        Add(e = new LightFadeTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "bloomFadeTrigger":
                        Add(e = new BloomFadeTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "cameraTargetTrigger": {
                            string text2 = entity3.Attr("deleteFlag");
                            if (string.IsNullOrEmpty(text2) || !Session.GetFlag(text2)) {
                                Add(e = new CameraTargetTrigger(entity3, vector));
                                (e as patch_Entity).__EntityData = entity3;
                            }
                            break;
                        }
                    case "cameraAdvanceTargetTrigger":
                        Add(e = new CameraAdvanceTargetTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "respawnTargetTrigger":
                        Add(e = new RespawnTargetTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "changeRespawnTrigger":
                        Add(e = new ChangeRespawnTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "windTrigger":
                        Add(e = new WindTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "windAttackTrigger":
                        Add(e = new WindAttackTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "minitextboxTrigger":
                        Add(e = new MiniTextboxTrigger(entity3, vector, new EntityID(levelData.Name, entity3.ID + 10000000)));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "oshiroTrigger":
                        Add(e = new OshiroTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "interactTrigger":
                        Add(e = new InteractTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "checkpointBlockerTrigger":
                        Add(e = new CheckpointBlockerTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "lookoutBlocker":
                        Add(e = new LookoutBlocker(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "stopBoostTrigger":
                        Add(e = new StopBoostTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "noRefillTrigger":
                        Add(e = new NoRefillTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "ambienceParamTrigger":
                        Add(e = new AmbienceParamTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "creditsTrigger":
                        Add(e = new CreditsTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "goldenBerryCollectTrigger":
                        Add(e = new GoldBerryCollectTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "moonGlitchBackgroundTrigger":
                        Add(e = new MoonGlitchBackgroundTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "blackholeStrength":
                        Add(e = new BlackholeStrengthTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "rumbleTrigger":
                        Add(e = new RumbleTrigger(entity3, vector, new EntityID(levelData.Name, entity3.ID + 10000000)));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "birdPathTrigger":
                        Add(e = new BirdPathTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "spawnFacingTrigger":
                        Add(e = new SpawnFacingTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    case "detachFollowersTrigger":
                        Add(e = new DetachStrawberryTrigger(entity3, vector));
                        (e as patch_Entity).__EntityData = entity3;
                        break;
                    default:
                        return false;

                }
                return true;
            }

        }
    }

    public static class LevelExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        internal static EventInstance PauseSnapshot => patch_Level._PauseSnapshot;

        public static SubHudRenderer GetSubHudRenderer(this Level self)
            => ((patch_Level) self).SubHudRenderer;
        public static void SetSubHudRenderer(this Level self, SubHudRenderer value)
            => ((patch_Level) self).SubHudRenderer = value;
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
            MethodDefinition m_LoadNewPlayer = context.Method.DeclaringType.FindMethod("Celeste.Player LoadNewPlayer(Microsoft.Xna.Framework.Vector2,Celeste.PlayerSpriteMode)");
            MethodDefinition m_LoadCustomEntity = context.Method.DeclaringType.FindMethod("System.Boolean LoadCustomEntity(Celeste.EntityData,Celeste.Level)");
            MethodDefinition m_PatchHeartGemBehavior = context.Method.DeclaringType.FindMethod("Celeste.AreaMode _PatchHeartGemBehavior(Celeste.AreaMode)");
            // These are used for the static constructor patch
            FieldDefinition f_LoadStrings = context.Method.DeclaringType.FindField("_LoadStrings");
            TypeReference t_LoadStrings = f_LoadStrings.FieldType;
            MethodReference m_LoadStrings_Add = MonoModRule.Modder.Module.ImportReference(t_LoadStrings.Resolve().FindMethod("Add"));
            MethodReference m_LoadStrings_ctor = MonoModRule.Modder.Module.ImportReference(t_LoadStrings.Resolve().FindMethod("System.Void .ctor()"));
            m_LoadStrings_Add.DeclaringType = t_LoadStrings;
            m_LoadStrings_ctor.DeclaringType = t_LoadStrings;
            // These are used for the EntityData patch
            MethodDefinition m_RegisterEntityDataWithEntity = context.Method.DeclaringType.FindMethod("Monocle.Entity RegisterEntityDataWithEntity(Monocle.Entity,Celeste.EntityData)");
            VariableDefinition entityData1 = context.Body.Variables.First(v => v.VariableType.FullName == "Celeste.EntityData");
            VariableDefinition entityData2 = context.Body.Variables.Last(v => v.VariableType.FullName == "Celeste.EntityData");
            VariableDefinition entityDataEnumerator = context.Body.Variables.First(v => v.VariableType.FullName == "System.Collections.Generic.List`1/Enumerator<Celeste.EntityData>");
            FieldReference f_temporaryEntityData = context.Method.DeclaringType.FindField("temporaryEntityData");
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
            //  After:  Player player = Level.LoadNewPlayer(this.Session.RespawnPoint.Value, spriteMode);
            cursor.GotoNext(instr => instr.MatchNewobj("Celeste.Player"));
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

            // EntityData patch: replaces all instances of adding a vanilla entity with that same entity with a reference to its entityData
            // Reset to apply EntityData patch
            cursor.Index = 0;
            int idx = entityData1.Index;
            cursor.GotoNext(MoveType.After, instr => instr.MatchStloc(16)); // Stloc.s 16 is used for both foreach loops
            cursor.Index++;
            while (!cursor.Next.MatchStloc(entityDataEnumerator.Index)) {
                if (cursor.Next.OpCode == OpCodes.Call && cursor.Next.Operand is MethodReference mr && mr.FullName == "System.Void Monocle.Scene::Add(Monocle.Entity)") { // This is the first thing that worked all night.
                    cursor.Emit(OpCodes.Ldloc, idx);
                    cursor.Emit(OpCodes.Call, m_RegisterEntityDataWithEntity);
                } else if(cursor.Next.OpCode == OpCodes.Call && cursor.Next.Operand is MethodReference mr2 && mr2.FullName == "System.Void Celeste.ClutterBlockGenerator::Init(Celeste.Level)") {
                    cursor.Index++; // Go after
                    cursor.Emit(OpCodes.Ldloc, idx);
                    cursor.Emit(OpCodes.Stsfld, f_temporaryEntityData);
                    cursor.GotoNext(MoveType.Before, i=>i.MatchBr(out _)); // This is stupid but since it's Everest directly it's fiiiiiine
                    cursor.Emit(OpCodes.Ldnull);
                    cursor.Emit(OpCodes.Stsfld, f_temporaryEntityData);
                }
                cursor.Index++;
            }
            idx = entityData2.Index;
            while (!cursor.Next.MatchEndfinally()) {
                if (cursor.Next.OpCode == OpCodes.Call && cursor.Next.Operand is MethodReference mr && mr.FullName == "System.Void Monocle.Scene::Add(Monocle.Entity)") { // This is the first thing that worked all night.
                    cursor.Emit(OpCodes.Ldloc, idx);
                    cursor.Emit(OpCodes.Call, m_RegisterEntityDataWithEntity);
                }
                cursor.Index++;
            }
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
            cursor.Emit(OpCodes.Brfalse, cursor.Next).Emit(OpCodes.Ret);

            // insert FixChaserStatesTimeStamp()
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
