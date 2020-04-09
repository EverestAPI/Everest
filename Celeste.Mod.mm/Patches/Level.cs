#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used
#pragma warning disable CS0414 // The field is assigned but its value is never used

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Entities;
using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.Linq;
using Celeste.Mod.Meta;
using MonoMod.Utils;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

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

        public new Vector2 DefaultSpawnPoint {
            [MonoModReplace]
            get {
                if (Session.Area.GetLevelSet() == "Celeste")
                    return GetSpawnPoint(new Vector2(Bounds.Left, Bounds.Bottom));

                return Session.LevelData.Spawns[0];
            }
        }

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
                SaveData.Instance.RegisterCompletion(this.Session);
                Everest.Events.Level.Complete(this);
            }
        }

        public extern void orig_DoScreenWipe(bool wipeIn, Action onComplete = null, bool hiresSnow = false);
        public new void DoScreenWipe(bool wipeIn, Action onComplete = null, bool hiresSnow = false) {
            if (onComplete == null && !hiresSnow && SkipScreenWipes > 0) {
                SkipScreenWipes--;
                return;
            }

            orig_DoScreenWipe(wipeIn, onComplete, hiresSnow);
        }

        // Needed for older mods.
        public void NextColorGrade(string next) {
            NextColorGrade(next, 1f);
        }

        public ScreenWipe CompleteArea(bool spotlightWipe = true, bool skipScreenWipe = false) {
            return CompleteArea(spotlightWipe, skipScreenWipe, false);
        }

        public extern void orig_Pause(int startIndex = 0, bool minimal = false, bool quickReset = false);
        public new void Pause(int startIndex = 0, bool minimal = false, bool quickReset = false) {
            orig_Pause(startIndex, minimal, quickReset);

            if (!quickReset) {
                TextMenu menu = Entities.GetToAdd().FirstOrDefault(e => e is TextMenu) as TextMenu;
                if (menu != null)
                    Everest.Events.Level.CreatePauseMenuButtons(this, menu, minimal);
            }

            Everest.Events.Level.Pause(this, startIndex, minimal, quickReset);
        }

        public extern void orig_TransitionTo(LevelData next, Vector2 direction);
        public new void TransitionTo(LevelData next, Vector2 direction) {
            orig_TransitionTo(next, direction);
            Everest.Events.Level.TransitionTo(this, next, direction);
        }

        private extern IEnumerator orig_TransitionRoutine(LevelData next, Vector2 direction);
        [PatchTransitionRoutine]
        private IEnumerator TransitionRoutine(LevelData next, Vector2 direction) {
            IEnumerator orig = orig_TransitionRoutine(next, direction);

            // Don't perform any GBJ checks in vanilla maps.
            if (Session.Area.GetLevelSet() == "Celeste" || CoreModule.Settings.DisableAntiSoftlock) {
                while (orig.MoveNext())
                    yield return orig.Current;
                yield break;
            }

            Player player = Tracker.GetEntity<Player>();
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

        public extern void orig_LoadLevel(Player.IntroTypes playerIntro, bool isFromLoader = false);
        [PatchLevelLoader] // Manually manipulate the method via MonoModRules
        public new void LoadLevel(Player.IntroTypes playerIntro, bool isFromLoader = false) {
            // Read player introType from metadata as player enter the C-Side
            if (Session.FirstLevel && Session.StartedFromBeginning && Session.JustStarted
                && Session.Area.Mode == AreaMode.CSide
                && AreaData.GetMode(Session.Area)?.GetMapMeta() is MapMeta mapMeta && (mapMeta.OverrideASideMeta ?? false)
                && mapMeta.IntroType is Player.IntroTypes introType)
                playerIntro = introType;

            try {
                orig_LoadLevel(playerIntro, isFromLoader);

                if (ShouldAutoPause) {
                    ShouldAutoPause = false;
                    Pause();
                }
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "LoadLevel", $"Failed loading level {Session.Area}");
                e.LogDetailed();

                string message = Dialog.Get("postcard_levelloadfailed");
                if (e is ArgumentOutOfRangeException && e.StackTrace.Contains("get_DefaultSpawnPoint"))
                    message = Dialog.Get("postcard_levelnospawn");
                message = message
                    .Replace("((player))", SaveData.Instance.Name)
                    .Replace("((sid))", Session.Area.GetSID());

                Entity helperEntity = new Entity();
                helperEntity.Add(new Coroutine(ErrorRoutine(message)));
                Add(helperEntity);
                return;
            }
            Everest.Events.Level.LoadLevel(this, playerIntro, isFromLoader);
        }

        private IEnumerator ErrorRoutine(string message) {
            yield return null;

            Audio.SetMusic(null);

            LevelEnterExt.ErrorMessage = message;
            LevelEnter.Go(new Session(Session?.Area ?? new AreaKey(1).SetSID("")), false);
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
                Logger.Log(LogLevel.Warn, "LoadLevel", $"Failed loading entity {entityData.Name}");
            }

            return false;
        }

        private static object _GCCollectLock = Tuple.Create(new object(), "Level Transition GC.Collect");
        private static void _GCCollect() {
            if (Everest.Flags.IsDisabled || !(CoreModule.Settings.MultithreadedGC ?? !Everest.Flags.IsMono)) {
                GC.Collect();
                return;
            }

            QueuedTaskHelper.Do(_GCCollectLock, () => {
                GC.Collect(1, GCCollectionMode.Forced, false);
            });
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
