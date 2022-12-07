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

                Logger.Log(LogLevel.Warn, "LoadLevel", $"Failed loading room {Session.LevelData.Name} of {Session.Area.GetSID()}");
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
            if (unpauseTimer > 0f && Tracker.GetEntity<Player>()?.ChaserStates is { } chaserStates) {
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

        private void CheckForErrors() {
            if (patch_LevelEnter.ErrorMessage != null) {
                LevelEnter.Go(Session, false);
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
    /// Patch leevel loading method to copy decal rotations from <see cref="Celeste.DecalData" /> instances into newly created <see cref="Celeste.Decal" /> entities.
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
        }

        public static void PatchLevelLoaderDecalCreation(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_DecalData = MonoModRule.Modder.FindType("Celeste.DecalData").Resolve();
            TypeDefinition t_Decal = MonoModRule.Modder.FindType("Celeste.Decal").Resolve();
            FieldDefinition f_DecalData_Rotation = t_DecalData.FindField("Rotation");
            MethodDefinition m_Decal_ctor = t_Decal.FindMethod("System.Void .ctor(System.String,Microsoft.Xna.Framework.Vector2,Microsoft.Xna.Framework.Vector2,System.Int32,System.Single)");

            ILCursor cursor = new ILCursor(context);

            int loc_decaldata = -1;
            int matches = 0;
            // move to just before each of the two Decal constructor calls (one for FGDecals and one for BGDecals), and obtain a reference to the DecalData local
            while (cursor.TryGotoNext(MoveType.After,
                                      instr => instr.MatchLdloc(out loc_decaldata),
                                      instr => instr.MatchLdfld("Celeste.DecalData", "Scale"),
                                      instr => instr.MatchLdcI4(Celeste.Depths.FGDecals)
                                            || instr.MatchLdcI4(Celeste.Depths.BGDecals))) {
                // we are trying to get:
                //   decal = new Decal()

                // load the rotation from the DecalData
                cursor.Emit(OpCodes.Ldloc_S, (byte) loc_decaldata);
                cursor.Emit(OpCodes.Ldfld, f_DecalData_Rotation);
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
