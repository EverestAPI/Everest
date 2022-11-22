using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    public class OuiModTogglerProgress : OuiLoggedProgress {

        private List<string> newlyUnblacklistedMods;

        public void Init(List<string> mods) {
            Everest.Loader.OnCrawlMod += logCrawlMod;
            newlyUnblacklistedMods = mods;
            Init<OuiMainMenu>(Dialog.Clean("MODOPTIONS_MODTOGGLE_PROGRESS_TITLE"), new Task(toggleMods),
                newlyUnblacklistedMods.Count);
        }

        public override IEnumerator Leave(Oui next) {
            Everest.Loader.OnCrawlMod -= logCrawlMod;
            newlyUnblacklistedMods = null;
            MainThreadHelper.Do(() => ((patch_OuiMainMenu) Overworld.GetUI<OuiMainMenu>())?.RebuildMainAndTitle());
            Audio.Play(SFX.ui_main_button_back);
            
            return base.Leave(next);
        }

        private void toggleMods() {
            if (newlyUnblacklistedMods == null)
                return;
            
            // give it a second to transition
            Thread.Sleep(1000);
            int oldDelayedMods = Everest.Loader.Delayed.Count;
            Everest.Loader.EnforceOptionalDependencies = true;
            foreach (string mod in newlyUnblacklistedMods) {
                try {
                    // remove the mod from the loaded blacklist & attempt to load mod
                    LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_MOD_UNBLACKLIST"), mod));
                    Everest.Loader._Blacklist.RemoveAll(item => item == mod);
                    
                    if (mod.EndsWith(".zip")) {
                        Everest.Loader.LoadZip(Path.Combine(Everest.Loader.PathMods, mod));
                    } else {
                        Everest.Loader.LoadDir(Path.Combine(Everest.Loader.PathMods, mod));
                    }

                    Progress += 1;
                } catch (Exception e) {
                    LogLine($"Failed to load {mod}!");
                    Logger.LogDetailed(e);
                }
            }
            
            LogLine(Dialog.Clean("MODOPTIONS_MODTOGGLE_PROGRESS_OPTIONAL"));
            Everest.Loader.EnforceOptionalDependencies = false;
            Everest.CheckDependenciesOfDelayedMods();

            if (Everest.Loader.Delayed.Count > oldDelayedMods) {
                LogLine(Dialog.Clean("MODOPTIONS_MODTOGGLE_PROGRESS_FAILED_LOAD"));
            }

            LogLine(Dialog.Clean("MODOPTIONS_MODTOGGLE_PROGRESS_MAINMENU"));
            for (int i = 3; i > 0; --i) {
                Lines[Lines.Count - 1] = string.Format(Dialog.Get("MODOPTIONS_MODTOGGLE_PROGRESS_MAINMENU_COUNTDOWN"), i);
                Thread.Sleep(1000);
            }
            Lines[Lines.Count - 1] = Dialog.Clean("MODOPTIONS_MODTOGGLE_PROGRESS_MAINMENU");
        }
        
        private void logCrawlMod(string filePath, EverestModuleMetadata meta) {
            if (meta != null) {
                LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_LOADING_MOD"), meta, filePath));
            } else {
                LogLine(string.Format(Dialog.Get("DEPENDENCYDOWNLOADER_LOADING_MOD_NOMETA"), filePath));
            }
        }
    }
}