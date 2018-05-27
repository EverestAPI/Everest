#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Entities;
using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Celeste {
    class patch_Level : Level {

        // We're effectively in GameLoader, but still need to "expose" private fields to our mod.
        private static EventInstance PauseSnapshot;
        public static EventInstance _PauseSnapshot => PauseSnapshot;

        public SubHudRenderer SubHudRenderer;

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchLevelRender] // ... except for manually manipulating the method via MonoModRules
        public override extern void Render();

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
                // Iterate over the added Entities and grab the first TextMenu.
                List<Entity> added = Entities.GetToAdd();
                foreach (Entity entity in added) {
                    if (!(entity is TextMenu))
                        continue;
                    TextMenu menu = (TextMenu) entity;
                    Everest.Events.Level.CreatePauseMenuButtons(this, menu, minimal);
                    break;
                }
            }

            Everest.Events.Level.Pause(this, startIndex, minimal, quickReset);
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
            if (Session.Area.GetSID() == "Celeste") {
                while (orig.MoveNext())
                    yield return orig.Current;
                yield break;
            }

            Player player = Tracker.GetEntity<Player>();

            Vector2 inside = direction * 4f;
            if (direction == Vector2.UnitY)
                inside = direction * 12f;

            Vector2 playerTo = player.Position;
            while (direction.X != 0f && playerTo.Y >= Bounds.Bottom)
                playerTo.Y -= 1f;
            while (!IsInBounds(playerTo, inside))
                playerTo += direction;

            Vector2 playerPos = player.Position;
            DateTime playerStuck = DateTime.UtcNow;

            while (orig.MoveNext()) {
                if (playerPos != player.Position)
                    playerStuck = DateTime.UtcNow;
                playerPos = player.Position;

                if ((DateTime.UtcNow - playerStuck).TotalSeconds > 2D && !player.TransitionTo(playerTo, direction)) {
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
            orig_LoadLevel(playerIntro, isFromLoader);
            Everest.Events.Level.LoadLevel(this, playerIntro, isFromLoader);
        }

        // Called from LoadLevel, patched via MonoModRules.PatchLevelLoader
        public static bool LoadCustomEntity(EntityData entityData, Level level) {
            LevelData levelData = level.Session.LevelData;
            Vector2 offset = new Vector2(levelData.Bounds.Left, levelData.Bounds.Top);

            if (Everest.Events.Level.LoadEntity(level, levelData, offset, entityData))
                return true;

            // Everest comes with a few core utility entities out of the box.

            if (entityData.Name == "levelFlagTrigger") {
                level.Add(new LevelFlagTrigger(entityData, offset));
                return true;
            }

            if (entityData.Name == "customCoreMessage") {
                level.Add(new CustomCoreMessage(entityData, offset));
                return true;
            }

            if (entityData.Name == "customMemorial") {
                level.Add(new CustomMemorial(entityData, offset));
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
