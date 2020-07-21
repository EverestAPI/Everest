using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;

namespace Celeste.Mod.UI {
    class OuiModToggler : OuiGenericMenu, OuiModOptions.ISubmenu {
        public override string MenuName => Dialog.Clean("MODOPTIONS_MODTOGGLE");

        // list of all mods in the Mods folder
        private List<string> allMods;
        // list of currently blacklisted mods
        private HashSet<string> blacklistedMods;
        // list of blacklisted mods when the menu was open
        private HashSet<string> blacklistedModsOriginal;

        private TextMenuExt.SubHeaderExt restartMessage1;
        private TextMenuExt.SubHeaderExt restartMessage2;

        public OuiModToggler() {
            backToParentMenu = onBackPressed;
        }

        protected override void addOptionsToMenu(TextMenu menu) {
            // if there is a whitelist, warn the user that it will break those settings.
            if (Everest.Loader.Whitelist != null) {
                menu.Add(restartMessage1 = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MODTOGGLE_WHITELISTWARN")) { TextColor = Color.OrangeRed });
            }

            // display the warning about blacklist.txt + restarting
            menu.Add(restartMessage1 = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MODTOGGLE_MESSAGE_1")));
            menu.Add(restartMessage2 = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MODTOGGLE_MESSAGE_2")) { HeightExtra = 0f });

            // reduce spacing between the whitelist warning and the blacklist overwrite warning
            if (Everest.Loader.Whitelist != null) {
                restartMessage1.HeightExtra = 30f;
            }

            // "enable all" and "disable all" buttons
            List<TextMenu.OnOff> allToggles = new List<TextMenu.OnOff>();
            menu.Add(new TextMenu.Button(Dialog.Clean("MODOPTIONS_MODTOGGLE_ENABLEALL")).Pressed(() => {
                foreach (TextMenu.OnOff toggle in allToggles) {
                    if (toggle.Index != 1) {
                        toggle.Index = 1;
                        toggle.OnValueChange(true);
                    }
                }
            }));
            menu.Add(new TextMenu.Button(Dialog.Clean("MODOPTIONS_MODTOGGLE_DISABLEALL")).Pressed(() => {
                foreach (TextMenu.OnOff toggle in allToggles) {
                    if (toggle.Index != 0) {
                        toggle.Index = 0;
                        toggle.OnValueChange(false);
                    }
                }
            }));

            // "cancel" button to leave the screen without saving
            menu.Add(new TextMenu.Button(Dialog.Clean("MODOPTIONS_MODTOGGLE_CANCEL")).Pressed(() => {
                blacklistedMods = blacklistedModsOriginal;
                onBackPressed(Overworld);
            }));

            // reset the mods list
            allMods = new List<string>();
            blacklistedMods = new HashSet<string>();

            string[] files;
            bool headerInserted;

            // crawl directories
            files = Directory.GetDirectories(Everest.Loader.PathMods);
            headerInserted = false;
            for (int i = 0; i < files.Length; i++) {
                string file = Path.GetFileName(files[i]);
                if (file != "Cache") {
                    if (!headerInserted) {
                        menu.Add(new TextMenu.SubHeader(Dialog.Clean("MODOPTIONS_MODTOGGLE_DIRECTORIES")));
                        headerInserted = true;
                    }
                    allToggles.Add(addFileToMenu(menu, file));
                }
            }

            // crawl zips
            files = Directory.GetFiles(Everest.Loader.PathMods);
            headerInserted = false;
            for (int i = 0; i < files.Length; i++) {
                string file = Path.GetFileName(files[i]);
                if (file.EndsWith(".zip")) {
                    if (!headerInserted) {
                        menu.Add(new TextMenu.SubHeader(Dialog.Clean("MODOPTIONS_MODTOGGLE_ZIPS")));
                        headerInserted = true;
                    }
                    allToggles.Add(addFileToMenu(menu, file));
                }
            }

            // crawl map bins
            files = Directory.GetFiles(Everest.Loader.PathMods);
            headerInserted = false;
            for (int i = 0; i < files.Length; i++) {
                string file = Path.GetFileName(files[i]);
                if (file.EndsWith(".bin")) {
                    if (!headerInserted) {
                        menu.Add(new TextMenu.SubHeader(Dialog.Clean("MODOPTIONS_MODTOGGLE_BINS")));
                        headerInserted = true;
                    }
                    allToggles.Add(addFileToMenu(menu, file));
                }
            }

            // sort the mods list alphabetically, for output in the blacklist.txt file later.
            allMods.Sort();

            // clone the list to be able to check if the list changed when leaving the menu.
            blacklistedModsOriginal = new HashSet<string>(blacklistedMods);
        }

        private TextMenu.OnOff addFileToMenu(TextMenu menu, string file) {
            TextMenu.OnOff option;

            bool enabled = !Everest.Loader._Blacklist.Contains(file);
            menu.Add(option = (TextMenu.OnOff) new TextMenu.OnOff(file.Length > 40 ? file.Substring(0, 40) + "..." : file, enabled)
                .Change(b => {
                    if (b) {
                        blacklistedMods.Remove(file);
                    } else {
                        blacklistedMods.Add(file);
                    }

                    if (blacklistedModsOriginal.SetEquals(blacklistedMods)) {
                        restartMessage1.TextColor = Color.Gray;
                        restartMessage2.TextColor = Color.Gray;
                    } else {
                        restartMessage1.TextColor = Color.OrangeRed;
                        restartMessage2.TextColor = Color.OrangeRed;
                    }
                }));

            allMods.Add(file);
            if (!enabled) {
                blacklistedMods.Add(file);
            }

            return option;
        }

        private void onBackPressed(Overworld overworld) {
            if (blacklistedModsOriginal.SetEquals(blacklistedMods)) {
                // nothing changed, go back to Mod Options
                overworld.Goto<OuiModOptions>();
            } else {
                // save the blacklist
                using (StreamWriter blacklistTxt = File.CreateText(Everest.Loader.PathBlacklist)) {
                    // header
                    blacklistTxt.WriteLine("# This is the blacklist. Lines starting with # are ignored.");
                    blacklistTxt.WriteLine("# File generated through the \"Toggle Mods\" menu in Mod Options");
                    blacklistTxt.WriteLine("");

                    // write all mods, commenting out the ones we want unblacklisted.
                    foreach (string mod in allMods) {
                        blacklistTxt.WriteLine(blacklistedMods.Contains(mod) ? mod : $"# {mod}");
                    }
                }

                // restart the game
                Everest.QuickFullRestart();
            }
        }
    }
}
