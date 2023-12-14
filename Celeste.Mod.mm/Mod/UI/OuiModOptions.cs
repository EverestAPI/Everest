using Celeste.Mod.Core;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Celeste.Mod.UI {
    public class OuiModOptions : Oui {

        /// <summary>
        /// Interface used to "tag" mod options submenus.
        /// </summary>
        public interface ISubmenu { }

        public static OuiModOptions Instance;

        private TextMenu menu;

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        private float alpha = 0f;

        private int savedMenuIndex = -1;

        private Action startSearching;

        public OuiModOptions() {
            Instance = this;
        }

        public static TextMenu CreateMenu(bool inGame, EventInstance snapshot) {
            patch_TextMenu menu = new patch_TextMenu();
            menu.CompactWidthMode = true;
            menu.BatchMode = true;

            menu.Add(new TextMenuExt.HeaderImage("menu/everest") {
                ImageColor = Color.White,
                ImageOutline = true,
                ImageScale = 0.5f
            });

            if (!inGame) {
                List<EverestModuleMetadata> missingDependencies = new List<EverestModuleMetadata>();

                lock (Everest.Loader.Delayed) {
                    if (Everest.Loader.Delayed.Count > 0 || Everest.Loader.ModsWithAssemblyLoadFailures.Count > 0) {
                        menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("modoptions_coremodule_notloaded_a")) { HeightExtra = 0f, TextColor = Color.OrangeRed });
                        menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("modoptions_coremodule_notloaded_b")) { HeightExtra = 0f, TextColor = Color.OrangeRed });

                        foreach (EverestModuleMetadata mod in Everest.Loader.ModsWithAssemblyLoadFailures) {
                            menu.Add(new TextMenuExt.SubHeaderExt($"{mod.Name} | v.{mod.VersionString} ({Dialog.Get("modoptions_coremodule_notloaded_asmloaderror")})") {
                                HeightExtra = 0f,
                                TextColor = Color.PaleVioletRed
                            });
                        }

                        foreach (Tuple<EverestModuleMetadata, Action> mod in Everest.Loader.Delayed) {
                            string missingDepsString = "";
                            if (mod.Item1.Dependencies != null) {
                                // check for missing dependencies
                                List<EverestModuleMetadata> missingDependenciesForMod = mod.Item1.Dependencies
                                    .FindAll(dep => !Everest.Loader.DependencyLoaded(dep));
                                if (mod.Item1.OptionalDependencies != null) {
                                    // find optional dependencies with mismatching versions
                                    List<EverestModuleMetadata> optionalDependenciesWithVersionMismatches = mod.Item1.OptionalDependencies
                                        .FindAll(dep => !Everest.Loader.DependencyLoaded(dep) && Everest.Modules.Any(module => module.Metadata?.Name == dep.Name));
                                    missingDependenciesForMod.AddRange(optionalDependenciesWithVersionMismatches);
                                }
                                missingDependencies.AddRange(missingDependenciesForMod);

                                if (missingDependenciesForMod.Count != 0) {
                                    // format their names and versions, and join all of them in a single string
                                    missingDepsString = string.Join(", ", missingDependenciesForMod.Select(dependency => dependency.Name + " | v." + dependency.VersionString));

                                    // ensure that string is not too long, or else it would break the display
                                    if (missingDepsString.Length > 40) {
                                        missingDepsString = missingDepsString.Substring(0, 40) + "...";
                                    }

                                    // wrap that in a " ({list} not found)" message
                                    missingDepsString = $" ({string.Format(Dialog.Get("modoptions_coremodule_notloaded_notfound"), missingDepsString)})";
                                }
                            }

                            menu.Add(new TextMenuExt.SubHeaderExt(mod.Item1.Name + " | v." + mod.Item1.VersionString + missingDepsString) {
                                HeightExtra = 0f,
                                TextColor = Color.PaleVioletRed
                            });
                        }
                    } else if (CoreModule.Settings.WarnOnEverestYamlErrors && Everest.Loader.FilesWithMetadataLoadFailures.Count > 0) {
                        menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("modoptions_coremodule_yamlerrors")) { HeightExtra = 0f, TextColor = Color.OrangeRed });
                        menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("modoptions_coremodule_notloaded_b")) { HeightExtra = 0f, TextColor = Color.OrangeRed });

                        foreach (string fileName in Everest.Loader.FilesWithMetadataLoadFailures) {
                            menu.Add(new TextMenuExt.SubHeaderExt(Path.GetFileName(fileName)) { HeightExtra = 0f, TextColor = Color.PaleVioletRed });
                        }
                    }
                }

                if (Everest.Updater.HasUpdate) {
                    menu.Add(new TextMenu.Button(Dialog.Clean("modoptions_coremodule_update").Replace("((version))", Everest.Updater.Newest.Build.ToString())).Pressed(() => {
                        Everest.Updater.Update(Instance.Overworld.Goto<OuiLoggedProgress>());
                    }));
                }

                if (missingDependencies.Count != 0) {
                    menu.Add(new TextMenu.Button(Dialog.Clean("modoptions_coremodule_downloaddeps")).Pressed(() => {
                        OuiDependencyDownloader.MissingDependencies = missingDependencies;
                        Instance.Overworld.Goto<OuiDependencyDownloader>();
                    }));
                }
            }

            // reorder Mod Options according to the modoptionsorder.txt file.
            List<EverestModule> modules = new List<EverestModule>(Everest._Modules);
            if (Everest.Loader._ModOptionsOrder != null && Everest.Loader._ModOptionsOrder.Count > 0) {
                foreach (string modName in Everest.Loader._ModOptionsOrder) {
                    if (modName.Equals("Everest", StringComparison.InvariantCultureIgnoreCase)) {
                        // the current entry is for Everest Core
                        createModMenuSectionAndDelete(modules, module => module.Metadata.Name == "Everest", menu, inGame, snapshot);
                    } else {
                        string modPath = Path.Combine(Everest.Loader.PathMods, modName);

                        // check for both PathDirectory and PathArchive for each module.
                        if (!createModMenuSectionAndDelete(modules, module => module.Metadata.PathDirectory == modPath, menu, inGame, snapshot)) {
                            createModMenuSectionAndDelete(modules, module => module.Metadata.PathArchive == modPath, menu, inGame, snapshot);
                        }
                    }
                }
            }

            foreach (EverestModule mod in modules)
                mod.CreateModMenuSection(menu, inGame, snapshot);

            if (menu.Height > menu.ScrollableMinSize) {
                menu.Position.Y = menu.ScrollTargetY;
            }

            ((patch_TextMenu) menu).BatchMode = false;
            return menu;
        }

        /// <summary>
        /// Adds the mod menu section for the first element of 'modules' that matches 'criteria',
        /// then removes it from the 'modules' list.
        /// </summary>
        /// <returns>true if an element matching 'criteria' was found, false otherwise.</returns>
        private static bool createModMenuSectionAndDelete(List<EverestModule> modules, Predicate<EverestModule> criteria,
            patch_TextMenu menu, bool inGame, EventInstance snapshot) {

            bool foundMatch = false;

            // find all modules matching the criteria, and create their mod menu sections.
            int matchingIndex = modules.FindIndex(criteria);
            while (matchingIndex != -1) {
                modules[matchingIndex].CreateModMenuSection(menu, inGame, snapshot);
                modules.RemoveAt(matchingIndex);
                foundMatch = true;
                matchingIndex = modules.FindIndex(criteria);
            }

            return foundMatch;
        }

        private void ReloadMenu() {
            Vector2 position = Vector2.Zero;

            int selected = -1;
            if (menu != null) {
                position = menu.Position;
                selected = menu.Selection;
                Scene.Remove(menu);
            }

            menu = CreateMenu(false, null);
            AddSearchBox(menu);

            if (selected >= 0) {
                menu.Selection = selected;
                menu.Position = position;
            }

            Scene.Add(menu);
        }

        private void AddSearchBox(TextMenu menu) {
            TextMenuExt.TextBox textBox = new(Overworld) {
                PlaceholderText = Dialog.Clean("MODOPTIONS_COREMODULE_SEARCHBOX_PLACEHOLDER")
            };

            TextMenuExt.Modal modal = new(absoluteY: 85, textBox);
            menu.Add(modal);

            startSearching = () => {
                // we want to ensure we don't open the search box while we are in a sub-menu
                if (menu.Focused) {
                    modal.Visible = true;
                    textBox.StartTyping();
                }
            };

            Action<TextMenuExt.TextBox> searchNextMod(bool inReverse) => (TextMenuExt.TextBox textBox) => {
                string searchTarget = textBox.Text.ToLower();
                List<TextMenu.Item> menuItems = ((patch_TextMenu) menu).Items;

                bool searchNextPredicate(TextMenu.Item item) {
                    string searchLabel = ((patch_TextMenu.patch_Item) item).SearchLabel();
                    return item.Visible && item.Selectable && !item.Disabled && searchLabel != null && searchLabel.ToLower().Contains(searchTarget);
                }


                if (TextMenuExt.TextBox.WrappingLinearSearch(menuItems, searchNextPredicate, menu.Selection + (inReverse ? -1 : 1), inReverse, out int targetSelectionIndex)) {
                    if (targetSelectionIndex >= menu.Selection) {
                        Audio.Play(SFX.ui_main_roll_down);
                    } else {
                        Audio.Play(SFX.ui_main_roll_up);
                    }

                    menu.Selection = targetSelectionIndex;
                } else {
                    Audio.Play(SFX.ui_main_button_invalid);
                }
            };

            void exitSearch(TextMenuExt.TextBox textBox) {
                textBox.StopTyping();
                modal.Visible = false;
                textBox.ClearText();
            }

            textBox.OnTextInputCharActions['\t'] = searchNextMod(false);
            textBox.OnTextInputCharActions['\n'] = (_) => { };
            textBox.OnTextInputCharActions['\r'] = (textBox) => {
                if (MInput.Keyboard.CurrentState.IsKeyDown(Keys.LeftShift)
                    || MInput.Keyboard.CurrentState.IsKeyDown(Keys.RightShift)) {
                    searchNextMod(true)(textBox);
                } else {
                    searchNextMod(false)(textBox);
                }
            };
            textBox.OnTextInputCharActions['\b'] = (textBox) => {
                if (textBox.DeleteCharacter()) {
                    Audio.Play(SFX.ui_main_rename_entry_backspace);
                } else {
                    exitSearch(textBox);
                    Input.MenuCancel.ConsumePress();
                }
            };


            textBox.AfterInputConsumed = () => {
                if (textBox.Typing) {
                    if (Input.ESC.Pressed) {
                        exitSearch(textBox);
                    } else if (Input.MenuDown.Pressed) {
                        searchNextMod(false)(textBox);
                    } else if (Input.MenuUp.Pressed) {
                        searchNextMod(true)(textBox);
                    }
                }
            };
        }

        public override IEnumerator Enter(Oui from) {
            ReloadMenu();

            // restore selection if coming from a submenu.
            if (savedMenuIndex != -1 && typeof(ISubmenu).IsAssignableFrom(from.GetType())) {
                menu.Selection = Math.Min(savedMenuIndex, menu.LastPossibleSelection);
                menu.Position.Y = menu.ScrollTargetY;
            }

            menu.Visible = Visible = true;
            menu.Focused = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = offScreenX + -1920f * Ease.CubeOut(p);
                alpha = Ease.CubeOut(p);
                yield return null;
            }

            menu.Focused = true;
        }

        public override IEnumerator Leave(Oui next) {
            Audio.Play(SFX.ui_main_whoosh_large_out);
            menu.Focused = false;

            // save the menu position in case we want to restore it.
            savedMenuIndex = menu.Selection;

            yield return Everest.SaveSettings();

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = onScreenX + 1920f * Ease.CubeIn(p);
                alpha = 1f - Ease.CubeIn(p);
                yield return null;
            }

            menu.Visible = Visible = false;
            menu.RemoveSelf();
            menu = null;
        }

        public override void Update() {
            if (menu != null && menu.Focused &&
                Selected && Input.MenuCancel.Pressed) {
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiMainMenu>();
            }

            if (Selected && Focused) {
                if (Input.QuickRestart.Pressed) {
                    startSearching?.Invoke();
                    return;
                }
            }

            base.Update();
        }

        public override void Render() {
            if (alpha > 0f)
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * alpha * 0.4f);


            MTexture searchIcon = GFX.Gui["menu/mapsearch"];

            const float PREFERRED_ICON_X = 100f;
            float spaceNearMenu = (Engine.Width - menu.Width) / 2;
            float scaleFactor = Math.Min(spaceNearMenu / (PREFERRED_ICON_X + searchIcon.Width / 2), 1);

            Vector2 searchIconLocation = new(PREFERRED_ICON_X * scaleFactor, 952f);
            searchIcon.DrawCentered(searchIconLocation, Color.White, scaleFactor);
            Input.GuiKey(Input.FirstKey(Input.QuickRestart)).Draw(searchIconLocation, Vector2.Zero, Color.White, scaleFactor);

            base.Render();
        }


    }
}
