#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using FMOD.Studio;
using Monocle;
using System.Collections.Generic;

namespace Celeste {
    class patch_Level : Level {

        // We're effectively in GameLoader, but still need to "expose" private fields to our mod.
        private static EventInstance PauseSnapshot;
        public static EventInstance _PauseSnapshot => PauseSnapshot;

        public extern void orig_Pause(int startIndex = 0, bool minimal = false, bool quickReset = false);
        public new void Pause(int startIndex = 0, bool minimal = false, bool quickReset = false) {
            orig_Pause(startIndex, minimal, quickReset);

            // Iterate over the added Entities and grab the first TextMenu.
            List<Entity> added = Entities.GetToAdd();
            foreach (TextMenu menu in added) {
                Everest.Events.Level.CreatePauseMenuButtons(this, menu, minimal);
                break;
            }

            Everest.Events.Level.Pause(this, startIndex, minimal, quickReset);
        }

    }
    public static class LevelExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        internal static EventInstance PauseSnapshot => patch_Level._PauseSnapshot;

    }
}
