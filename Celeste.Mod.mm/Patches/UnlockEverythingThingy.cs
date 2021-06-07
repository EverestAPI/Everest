using Monocle;
using MonoMod;

namespace Celeste {
    class patch_UnlockEverythingThingy : UnlockEverythingThingy {

        [MonoModReplace]
        public new void UnlockEverything(Level level) {
            patch_SaveData data = (patch_SaveData) SaveData.Instance;


            if (data.LevelSet == "Celeste") {
                foreach (LevelSetStats set in data.LevelSets)
                    set.UnlockedAreas = set.MaxArea;

                SaveData.Instance.RevealedChapter9 = true;
                Settings.Instance.VariantsUnlocked = true;
                Settings.Instance.Pico8OnMainMenu = true;

            } else {
                data.LevelSetStats.UnlockedAreas = data.LevelSetStats.MaxArea;
            }

            data.CheatMode = true;

            level.Session.InArea = false;

            Engine.Scene = new LevelExit(LevelExit.Mode.GiveUp, level.Session);
        }

    }
}
