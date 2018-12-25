#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

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
using System.Collections.Generic;
using System.Linq;

namespace Celeste {
    class patch_Level : Level {

        // We're effectively in GameLoader, but still need to "expose" private fields to our mod.
        private static EventInstance PauseSnapshot;
        public static EventInstance _PauseSnapshot => PauseSnapshot;

        public SubHudRenderer SubHudRenderer;

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

        public extern void orig_RegisterAreaComplete();
        public new void RegisterAreaComplete() {
            bool completed = Completed;
            orig_RegisterAreaComplete();
            if (!completed) {
                Everest.Events.Level.Complete(this);
            }
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

        private extern void orig_GiveUp(int returnIndex, bool restartArea, bool minimal, bool showHint);
        private void GiveUp(int returnIndex, bool restartArea, bool minimal, bool showHint) {
            GiveUpHint hint = null;
            if (!Everest.Flags.IsDisabled && !restartArea && !showHint) {
                // The game originally doesn't show a hint when exiting to the map.
                Add(hint = new GiveUpHint());
            }

            orig_GiveUp(returnIndex, restartArea, minimal, showHint);

            TextMenu menu = Entities.GetToAdd().LastOrDefault(e => e is TextMenu) as TextMenu;
            if (menu == null)
                return;

            Action removeHint = () => hint?.RemoveSelf();
            menu.OnPause += removeHint;
            menu.OnESC += removeHint;
            menu.OnCancel += removeHint;
        }

        public extern void orig_TransitionTo(LevelData next, Vector2 direction);
        public new void TransitionTo(LevelData next, Vector2 direction) {
            orig_TransitionTo(next, direction);
            Everest.Events.Level.TransitionTo(this, next, direction);
        }

        private extern IEnumerator orig_TransitionRoutine(LevelData next, Vector2 direction);
        private IEnumerator TransitionRoutine(LevelData next, Vector2 direction) {
            IEnumerator orig = orig_TransitionRoutine(next, direction);

            // Don't perform any Gay Baby Jail checks in vanilla maps.
            if (Session.Area.GetLevelSet() == "Celeste") {
                while (orig.MoveNext())
                    yield return orig.Current;
                yield break;
            }

            Player player = Tracker.GetEntity<Player>();

            Vector2 playerPos = player.Position;
            DateTime playerStuck = DateTime.UtcNow;

            while (orig.MoveNext()) {
                if (playerPos != player.Position)
                    playerStuck = DateTime.UtcNow;
                playerPos = player.Position;

                if ((DateTime.UtcNow - playerStuck).TotalSeconds >= 5D) {
                    // Player stuck in Gay Baby Jail - force-reload the level.
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
            try {
                orig_LoadLevel(playerIntro, isFromLoader);
            } catch (Exception e) {
                Mod.Logger.Log(LogLevel.Warn, "misc", $"Failed loading level {Session.Area}");
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
            LevelEnter.Go(new Session(new AreaKey(1).SetSID("")), false);
        }

        // Called from LoadLevel, patched via MonoModRules.PatchLevelLoader
        public static bool LoadCustomEntity(EntityData entityData, Level level) {
            LevelData levelData = level.Session.LevelData;
            Vector2 offset = new Vector2(levelData.Bounds.Left, levelData.Bounds.Top);

            if (Everest.Events.Level.LoadEntity(level, levelData, offset, entityData))
                return true;

            // Everest comes with a few core utility entities out of the box.

            if (entityData.Name == "everest/spaceController") {
                level.Add(new SpaceController());
                return true;
            }
            if (entityData.Name == "everest/spaceControllerBlocker") {
                level.Add(new SpaceControllerBlocker());
                return true;
            }

            if (entityData.Name == "everest/flagTrigger") {
                level.Add(new FlagTrigger(entityData, offset));
                return true;
            }

            if (entityData.Name == "everest/coreMessage") {
                level.Add(new CustomCoreMessage(entityData, offset));
                return true;
            }

            if (entityData.Name == "everest/memorial") {
                level.Add(new CustomMemorial(entityData, offset));
                return true;
            }

            if (entityData.Name == "everest/dialogTrigger" ||
                entityData.Name == "dialog/dialogtrigger") {
                level.Add(new DialogCutsceneTrigger(entityData, offset, new EntityID(levelData.Name, entityData.ID)));
                return true;
            }

            if (entityData.Name == "everest/crystalShatterTrigger" ||
                entityData.Name == "outback/destroycrystalstrigger") {
                level.Add(new CrystalShatterTrigger(entityData, offset));
                return true;
            }

            if (entityData.Name == "everest/completeAreaTrigger" ||
                entityData.Name == "outback/completeareatrigger") {
                level.Add(new CompleteAreaTrigger(entityData, offset));
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
                else if ("core".Equals(entityData.Attr("color"), StringComparison.InvariantCultureIgnoreCase))
                    color = (CrystalColor) (-1);
                else if (!Enum.TryParse(entityData.Attr("color"), true, out color))
                    color = CrystalColor.Blue;

                level.Add(new CrystalStaticSpinner(entityData, offset, color));
                return true;
            }

            if (entityData.Name == "trackSpinner") {
                if (level.Session.Area.ID == 3 ||
                    (level.Session.Area.ID == 7 && level.Session.Level.StartsWith("d-")) ||
                    entityData.Bool("dust")) {
                    level.Add(new DustTrackSpinner(entityData, offset));
                    return true;
                }

                level.Add(new BladeTrackSpinner(entityData, offset));
                return true;
            }

            if (entityData.Name == "rotateSpinner") {
                if (level.Session.Area.ID == 3 ||
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

            if (entityData.Name == "triggerSpikesOriginalUp") {
                level.Add(new TriggerSpikesOriginal(entityData, offset, TriggerSpikesOriginal.Directions.Up));
                return true;
            }
            if (entityData.Name == "triggerSpikesOriginalDown") {
                level.Add(new TriggerSpikesOriginal(entityData, offset, TriggerSpikesOriginal.Directions.Down));
                return true;
            }
            if (entityData.Name == "triggerSpikesOriginalLeft") {
                level.Add(new TriggerSpikesOriginal(entityData, offset, TriggerSpikesOriginal.Directions.Left));
                return true;
            }
            if (entityData.Name == "triggerSpikesOriginalRight") {
                level.Add(new TriggerSpikesOriginal(entityData, offset, TriggerSpikesOriginal.Directions.Right));
                return true;
            }

            if (entityData.Name == "darkChaserEnd") {
                level.Add(new BadelineOldsiteEnd(entityData, offset));
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
                    cobweb.OverrideColor = entityData.HexColor("color");
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

            return false;
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
