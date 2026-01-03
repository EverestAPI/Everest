using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using FMOD.Studio;
using MAB.DotIgnore;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Celeste.Mod.UI {
    public class OuiModOptions : Oui {

        /// <summary>
        /// Interface used to "tag" mod options submenus.
        /// </summary>
        public interface ISubmenu { }

        public static OuiModOptions Instance;

        private TextMenu rootMenu;
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
            patch_TextMenu rootMenu = new patch_TextMenu();
            rootMenu.CompactWidthMode = true;
            rootMenu.BatchMode = true;
            TextMenuExt.HeaderImage headerImage = new TextMenuExt.HeaderImage("menu/everest") {
                ImageColor = Color.White,
                ImageOutline = true,
                ImageScale = 0.5f,
            };
            rootMenu.Add(headerImage);

            List<EverestModule> modules = new List<EverestModule>(Everest._Modules);
            // sort by Mod names and move everest to the beginning
            modules.Remove(CoreModule.Instance);
            modules.Sort((a, b) => a.Metadata.Name.CompareTo(b.Metadata.Name));
            modules.Insert(0, CoreModule.Instance);

            List<EverestModule> orderRequired = new List<EverestModule>();
            // handle ordered mod separately according to the modoptionsorder.txt file
            List<string> orderList = Everest.Loader._ModOptionsOrder;
            if (orderList?.Count > 0) {
                foreach (string modName in orderList) {
                    if (modName.Equals("Everest", StringComparison.InvariantCultureIgnoreCase)) {
                        orderRequired.Add(CoreModule.Instance);
                    }
                    string modPath = Path.Combine(Everest.Loader.PathMods, modName);
                    orderRequired.AddRange(modules.Where(
                        m => m.Metadata.PathDirectory == modPath ||
                             m.Metadata.PathArchive == modPath
                             ));
                }
            }

            modules.RemoveAll(orderRequired.Contains);

            // now create our mod options

            if (!inGame)
                CreateCoreModuleNotInGameSection(rootMenu);

            bool nested = CoreModule.Settings.NestedOptions;
            if (nested) {
                foreach (EverestModule module in orderRequired.Concat(modules)) {
                    if (module._Settings == null || module.SettingsType == null)
                        continue;
                    patch_TextMenu menu = new patch_TextMenu();
                    menu.CompactWidthMode = true;

                    menu.BatchMode = true;
                    module.CreateModMenuSection(menu, inGame, snapshot);
                    menu.BatchMode = false;

                    if (menu.Items.Count == 0)
                        continue;

                    // some mods will have a disabled item
                    // as their first menu item
                    menu.FirstSelection();

                    string modSettingsName = module.SettingsType.Name.ToLowerInvariant();
                    if (modSettingsName.EndsWith("settings") == true)
                        modSettingsName = modSettingsName[..^8];

                    string title = (module.SettingsType.GetCustomAttribute<SettingNameAttribute>()?.Name
                        ?? $"modoptions_{modSettingsName}_title").DialogCleanOrNull()
                        ?? ModUpdaterHelper.FormatModName(module.Metadata.Name);
                    TextMenu.Button button = new TextMenu.Button(title);

                    menu.OnESC = () => {
                        Scene scene = menu.Scene;
                        scene.Add(rootMenu);
                        scene.Remove(menu);

                        rootMenu.Focused = true;
                        menu.Focused = false;
                        if (!inGame)
                            Instance.menu = rootMenu;
                    };
                    menu.OnCancel = menu.OnESC;
                    button.Pressed(() => {
                        Scene scene = button.Container.Scene;
                        scene.Add(menu);
                        scene.Remove(rootMenu);
                        rootMenu.Focused = false;
                        menu.Focused = true;
                        if (!inGame)
                            Instance.menu = menu;
                    });

                    rootMenu.Add(button);
                }
            } else {
                foreach (EverestModule module in orderRequired.Concat(modules)) {
                    module.CreateModMenuSection(rootMenu, inGame, snapshot);
                }
            }
            if (rootMenu.Height > rootMenu.ScrollableMinSize) {
                rootMenu.Position.Y = rootMenu.ScrollTargetY;
            }

            rootMenu.BatchMode = false;

            // there'll be a narrow menu when nested options is enabled
            // then the header image will be ugly if we don't center it

            // 1640f is the 'real' width of the 'everest/menu' texture
            float headerImageWidth = 1640f * headerImage.ImageScale;

            if (rootMenu.Width < headerImageWidth) {
                headerImage.Offset = new Vector2(-headerImageWidth / 2f + rootMenu.Width / 2f, 0f);
            }

            return rootMenu;
        }

        private static void CreateCoreModuleNotInGameSection(TextMenu menu) {
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

        private void ReloadMenu() {
            Vector2 position = Vector2.Zero;

            int selected = -1;
            if (menu != null) {
                position = menu.Position;
                selected = menu.Selection;
                Scene.Remove(menu);
            }

            menu = rootMenu = CreateMenu(false, null);
            startSearching = AddSearchBox(menu, Overworld);

            if (selected >= 0) {
                menu.Selection = selected;
                menu.Position = position;
            }

            Scene.Add(menu);
        }

        static public Action AddSearchBox(TextMenu menu, Overworld overworld = null) {
            TextMenuExt.TextBox textBox = new(overworld) {
                PlaceholderText = Dialog.Clean("MODOPTIONS_COREMODULE_SEARCHBOX_PLACEHOLDER")
            };

            TextMenuExt.Modal modal = new(textBox, absoluteX: null, absoluteY: 85);
            menu.Add(modal);
            menu.Add(new TextMenuExt.SearchToolTip());

            Action<TextMenuExt.TextBox> searchNextMod(bool inReverse) => (TextMenuExt.TextBox textBox) => {
                string searchTarget = textBox.Text.ToLower();
                List<TextMenu.Item> menuItems = ((patch_TextMenu) menu).Items;

                bool searchNextPredicate(TextMenu.Item item) {
                    if (!item.Visible || !item.Selectable || item.Disabled)
                        return false;
                    int index = menu.IndexOf(item);
                    if (index > 0 && (menu as patch_TextMenu).Items[index - 1] is patch_TextMenu.patch_SubHeader subHeader) {
                        if (subHeader.Title != null && subHeader.Title.ToLower().Contains(searchTarget)) {
                            return true;
                        }
                    }
                    string searchLabel = ((patch_TextMenu.patch_Item) item).SearchLabel();
                    return searchLabel != null && searchLabel.ToLower().Contains(searchTarget);
                }

                if (TextMenuExt.TextBox.WrappingLinearSearch(menuItems, searchNextPredicate, menu.Selection + (inReverse ? -1 : 1), inReverse, out int targetSelectionIndex)) {
                    if (targetSelectionIndex >= menu.Selection) {
                        Audio.Play(SFX.ui_main_roll_down);
                    } else {
                        Audio.Play(SFX.ui_main_roll_up);
                    }
                    menuItems[menu.Selection].OnLeave?.Invoke();
                    menu.Selection = targetSelectionIndex;
                    menuItems[targetSelectionIndex].OnEnter?.Invoke();
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
                        Input.ESC.ConsumePress();
                    } else if (Input.MenuDown.Pressed) {
                        searchNextMod(false)(textBox);
                    } else if (Input.MenuUp.Pressed) {
                        searchNextMod(true)(textBox);
                    }
                }
            };

            return () => {
                // we want to ensure we don't open the search box while we are in a sub-menu
                if (menu.Focused) {
                    modal.Visible = true;
                    textBox.StartTyping();
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
            if (rootMenu != null && rootMenu.Focused &&
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
            base.Render();
        }


    }
}
