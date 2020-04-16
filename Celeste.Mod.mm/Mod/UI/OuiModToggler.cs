using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Celeste.Mod.UI {
    class OuiModToggler : OuiGenericMenu, OuiModOptions.ISubmenu {
        public override string MenuName => Dialog.Clean("MODOPTIONS_MODTOGGLE");

        private HashSet<string> blacklistedMods;
        private HashSet<string> blacklistedModsOriginal;

        private TextMenuExt.SubHeaderExt restartMessage;
        private TextMenuExt.SubHeaderExt restartMessage2;

        public OuiModToggler() {
            backToParentMenu = onBackPressed;
        }

        protected override void addOptionsToMenu(TextMenu menu) {

            menu.Add(restartMessage = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MODTOGGLE_MESSAGE")));
            menu.Add(restartMessage2 = new TextMenuExt.SubHeaderExt(Dialog.Clean("MODOPTIONS_MODTOGGLE_MESSAGE2")) { HeightExtra = 0f });

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
            blacklistedMods = new HashSet<string>();

            // crawl zips
            menu.Add(new TextMenu.SubHeader(Dialog.Clean("MODOPTIONS_MODTOGGLE_ZIPS")));
            string[] files = Directory.GetFiles(Everest.Loader.PathMods);
            for (int i = 0; i < files.Length; i++) {
                string file = Path.GetFileName(files[i]);
                if (file.EndsWith(".zip")) {
                    allToggles.Add(addFileToMenu(menu, file));
                }
            }

            // crawl directories
            menu.Add(new TextMenu.SubHeader(Dialog.Clean("MODOPTIONS_MODTOGGLE_DIRECTORIES")));
            files = Directory.GetDirectories(Everest.Loader.PathMods);
            for (int i = 0; i < files.Length; i++) {
                string file = Path.GetFileName(files[i]);
                if (file != "Cache") {
                    allToggles.Add(addFileToMenu(menu, file));
                }
            }

            // clone the list to be able to check if the list changed when leaving the menu.
            blacklistedModsOriginal = new HashSet<string>(blacklistedMods);
        }

        private TextMenu.OnOff addFileToMenu(TextMenu menu, string file) {
            TextMenu.OnOff option;

            bool enabled = !Everest.Loader._Blacklist.Contains(file) && (Everest.Loader._Whitelist == null || Everest.Loader._Whitelist.Contains(file));
            menu.Add(option = (TextMenu.OnOff) new TextMenu.OnOff(file.Length > 40 ? file.Substring(0, 40) + "..." : file, enabled)
                .Change(b => {
                    if (b) {
                        blacklistedMods.Remove(file);
                    } else {
                        blacklistedMods.Add(file);
                    }

                    if(blacklistedModsOriginal.SetEquals(blacklistedMods)) {
                        restartMessage.TextColor = Color.Gray;
                        restartMessage2.TextColor = Color.Gray;
                    } else {
                        restartMessage.TextColor = Color.OrangeRed;
                        restartMessage2.TextColor = Color.OrangeRed;
                    }
                }));

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
                List<string> blacklistedList = blacklistedMods.ToList();
                blacklistedList.Sort();
                blacklistedList.Insert(0, "# This is the blacklist. Lines starting with # are ignored.");
                blacklistedList.Insert(1, "# File generated through the \"Toggle Mods\" menu in Mod Options");
                blacklistedList.Insert(2, "");
                File.WriteAllLines(Everest.Loader.PathBlacklist, blacklistedList);

                // delete the whitelist
                if (File.Exists(Everest.Loader.PathWhitelist)) {
                    File.Delete(Everest.Loader.PathWhitelist);
                }

                // restart the game
                Everest.QuickFullRestart();
            }
        }
    }
}
