#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Monocle;
using MonoMod;

namespace Celeste {
    class patch_UnlockEverythingThingy : UnlockEverythingThingy {

        [MonoModReplace]
        public new void UnlockEverything(Level level) {
            patch_SaveData data = (patch_SaveData) SaveData.Instance;
            foreach (LevelSetStats set in data.LevelSets) {
                set.UnlockedAreas = SaveData.Instance.MaxArea;
            }
            data.CheatMode = true;

            Settings.Instance.Pico8OnMainMenu = true;
            Settings.Instance.VariantsUnlocked = true;

            level.Session.InArea = false;

            Engine.Scene = new LevelExit(LevelExit.Mode.GiveUp, level.Session);
        }

    }
}
