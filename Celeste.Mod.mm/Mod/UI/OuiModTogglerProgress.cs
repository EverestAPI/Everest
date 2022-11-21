using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    public class OuiModTogglerProgress : OuiLoggedProgress {
        public override IEnumerator Enter(Oui from) {
            Everest.Loader.OnCrawlMod += logCrawlMod;
            Init<OuiMainMenu>(Dialog.Clean("MODOPTIONS_MODTOGGLE_PROGRESS"), new Task(toggleMods),
                Everest.Loader.TemporaryUntilIFigureOutWhereToPutThis.Count());
            
            return base.Enter(from);
        }

        public override IEnumerator Leave(Oui next) {
            Everest.Loader.OnCrawlMod -= logCrawlMod;
            Everest.Loader.TemporaryUntilIFigureOutWhereToPutThis = null;
            MainThreadHelper.Do(() => ((patch_OuiMainMenu) Overworld.GetUI<OuiMainMenu>())?.RebuildMainAndTitle());
            
            return base.Leave(next);
        }

        private void toggleMods() {
            // give it a second
            Thread.Sleep(1000);
            int oldDelayedMods = Everest.Loader.Delayed.Count;
            Everest.Loader.EnforceOptionalDependencies = true;
            foreach (string mod in Everest.Loader.TemporaryUntilIFigureOutWhereToPutThis) {
                try {
                    // remove the mod from the loaded blacklist & attempt to load mod
                    LogLine($"Removing mod {mod} from the blacklist...");
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
            
            LogLine("Loading mods with unsatisfied optional dependencies (if any)...");
            Everest.Loader.EnforceOptionalDependencies = false;
            Everest.CheckDependenciesOfDelayedMods();

            if (Everest.Loader.Delayed.Count > oldDelayedMods) {
                LogLine("Failed to load some mods! Check Mod Options menu for more info.");
            }

            LogLine(Dialog.Clean("Going to main menu"));
            for (int i = 3; i > 0; --i) {
                Lines[Lines.Count - 1] = $"Going to main menu in {i}";
                Thread.Sleep(1000);
            }
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