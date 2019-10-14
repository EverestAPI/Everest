using Celeste.Editor;
using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using Celeste;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using Celeste.Mod.Helpers;
using MonoMod.Utils;
using Microsoft.Xna.Framework.Input;
using System.Threading;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Celeste.Mod.Core {
    /// <summary>
    /// The Everest core module class. Feel free to access the core module settings from your own mod.
    /// </summary>
    public class CoreModule : EverestModule {

        public static CoreModule Instance;

        public override Type SettingsType => typeof(CoreModuleSettings);
        // Default values for anything using Settings too early (f.e. --dump-all)
        public static CoreModuleSettings Settings => (CoreModuleSettings) Instance?._Settings ?? new CoreModuleSettings();

        public override Type SaveDataType => typeof(CoreModuleSaveData);
        public static CoreModuleSaveData SaveData => (CoreModuleSaveData) Instance._SaveData;

        public override Type SessionType => typeof(CoreModuleSession);
        public static CoreModuleSession Session => (CoreModuleSession) Instance._Session;

        public CoreModule() {
            Instance = this;

            // Runtime modules shouldn't do this.
            Metadata = new EverestModuleMetadata() {
                Name = "Everest",
                VersionString = Everest.VersionString
            };
        }

        public override void LoadSettings() {
            base.LoadSettings();

            // If we're running in an environment that prefers those flag, forcibly enable them.
            Settings.LazyLoading |= Everest.Flags.PreferLazyLoading;

            // If using FNA with DISABLE_THREADING, forcibly enable non-threaded GL.
            // Note: This isn't accurate, as it doesn't check which GL device is being used.
            Type t_OpenGLDevice = typeof(Game).Assembly.GetType("Microsoft.Xna.Framework.Graphics.OpenGLDevice");
            if (typeof(Game).Assembly.FullName.Contains("FNA") &&
                t_OpenGLDevice != null &&
                t_OpenGLDevice.GetMethod("ForceToMainThread", BindingFlags.NonPublic | BindingFlags.Instance) == null) {
                Settings.NonThreadedGL = true;
            }
        }

        public override void Load() {
            Everest.Events.MainMenu.OnCreateButtons += CreateMainMenuButtons;
            Everest.Events.Level.OnCreatePauseMenuButtons += CreatePauseMenuButtons;

            if (Everest.Flags.IsMobile) {
                // It shouldn't look that bad on mobile screens...
                Environment.SetEnvironmentVariable("FNA_OPENGL_BACKBUFFER_SCALE_NEAREST", "1");
            }
        }

        public override void Initialize() {
            // F5: Reload and restart the current screen.
            Engine.Commands.FunctionKeyActions[4] = () => {
                // CTRL + F5: Quick-restart the entire game.
                if (MInput.Keyboard.Check(Keys.LeftControl) ||
                    MInput.Keyboard.Check(Keys.RightControl)) {
                    Everest.QuickFullRestart();
                    return;
                }

                Level level = Engine.Scene as Level;
                if (level == null)
                    return;

                AssetReloadHelper.Do("Reloading map", () => {
                    AreaData.Areas[level.Session.Area.ID].Mode[(int) level.Session.Area.Mode].MapData.Reload();
                });
                AssetReloadHelper.ReloadLevel();
            };

            // F6: Open map editor for current level.
            Engine.Commands.FunctionKeyActions[5] = () => {
                Level level = Engine.Scene as Level;
                if (level == null)
                    return;
                Engine.Scene = new MapEditor(level.Session.Area);
                Engine.Commands.Open = false;
            };

            // Set up the touch input regions.
            TouchRegion touchTitleScreen = new TouchRegion {
                Position = new Vector2(1920f, 1080f) * 0.5f,
                Size = new Vector2(1920f, 1080f),
                Condition = _ =>
                    ((Engine.Scene as Overworld)?.IsCurrent<OuiTitleScreen>() ?? false) ||
                    (Engine.Scene is GameLoader)
                ,
                Button = Input.MenuConfirm
            };
        }

        public override void LoadContent(bool firstLoad) {
            // Check if the current input GUI override is still valid. If so, apply it.
            if (!string.IsNullOrEmpty(Settings.InputGui)) {
                string inputGuiPath = $"controls/{Settings.InputGui}/";
                if (GFX.Gui.GetTextures().Any(kvp => kvp.Key.StartsWith(inputGuiPath))) {
                    Input.OverrideInputPrefix = Settings.InputGui;
                } else {
                    Settings.InputGui = "";
                }
            }

            if (firstLoad && !Everest.Flags.AvoidRenderTargets) {
                SubHudRenderer.Buffer = VirtualContent.CreateRenderTarget("subhud-target", 1922, 1082);
            }
            if (Everest.Flags.AvoidRenderTargets && Celeste.HudTarget != null) {
                Celeste.HudTarget.Dispose();
                Celeste.HudTarget = null;
            }

            if (Settings.NonThreadedGL) {
                GFX.Load();
                MTN.Load();
                GFX.LoadData();
                MTN.LoadData();
            }
            // Otherwise loaded in GameLoader.LoadThread

            // Celeste 1.3.0.0 gets rid of those.
            for (int i = 0; i <= 29; i++)
                GFX.Game[$"objects/checkpoint/flag{i:D2}"] = GFX.Game["util/pixel"];
            for (int i = 0; i <= 27; i++)
                GFX.Game[$"objects/checkpoint/obelisk{i:D2}"] = GFX.Game["util/pixel"];
        }

        public override void Unload() {
            Everest.Events.MainMenu.OnCreateButtons -= CreateMainMenuButtons;
            Everest.Events.Level.OnCreatePauseMenuButtons -= CreatePauseMenuButtons;

        }

        public void CreateMainMenuButtons(OuiMainMenu menu, List<MenuButton> buttons) {
            if (Everest.Flags.IsDisabled)
                return;

            int index;

            // Find the options button and place our button below it.
            index = buttons.FindIndex(_ => {
                MainMenuSmallButton other = (_ as MainMenuSmallButton);
                if (other == null)
                    return false;
                return other.GetLabelName() == "menu_options" && other.GetIconName() == "menu/options";
            });
            if (index != -1)
                index++;
            // Otherwise, place it above the exit button.
            else
                index = buttons.Count - 1;

            buttons.Insert(index, new MainMenuSmallButton("menu_modoptions", "menu/modoptions_new", menu, Vector2.Zero, Vector2.Zero, () => {
                Audio.Play(SFX.ui_main_button_select);
                Audio.Play(SFX.ui_main_whoosh_large_in);
                menu.Overworld.Goto<OuiModOptions>();
            }));
        }

        public void CreatePauseMenuButtons(Level level, TextMenu menu, bool minimal) {
            if (Everest.Flags.IsDisabled || !Settings.ShowModOptionsInGame)
                return;

            List<TextMenu.Item> items = menu.GetItems();
            int index;

            // Find the options button and place our button below it.
            string cleanedOptions = Dialog.Clean("menu_pause_options");
            index = items.FindIndex(_ => {
                TextMenu.Button other = (_ as TextMenu.Button);
                if (other == null)
                    return false;
                return other.Label == cleanedOptions;
            });
            if (index != -1)
                index++;
            // Otherwise, place it below the last button.
            else
                index = items.Count;

            TextMenu.Item itemModOptions = null;
            menu.Insert(index, itemModOptions = new TextMenu.Button(Dialog.Clean("menu_pause_modoptions")).Pressed(() => {
                int returnIndex = menu.IndexOf(itemModOptions);
                menu.RemoveSelf();

                level.PauseMainMenuOpen = false;
                level.Paused = true;

                TextMenu options = OuiModOptions.CreateMenu(true, LevelExt.PauseSnapshot);

                options.OnESC = options.OnCancel = () => {
                    Audio.Play(SFX.ui_main_button_back);
                    options.CloseAndRun(Everest.SaveSettings(), () => level.Pause(returnIndex, minimal, false));
                };

                options.OnPause = () => {
                    Audio.Play(SFX.ui_main_button_back);
                    options.CloseAndRun(Everest.SaveSettings(), () => {
                        level.Paused = false;
                        Engine.FreezeTimer = 0.15f;
                    });
                };

                level.Add(options);
            }));
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            // Optional - reload mod settings when entering the mod options.
            LoadSettings();

            if (!inGame) {
                lock (Everest.Loader.Delayed) {
                    if (Everest.Loader.Delayed.Count > 0) {
                        menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("modoptions_coremodule_notloaded_a")) { HeightExtra = 0f, TextColor = Color.OrangeRed });
                        menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("modoptions_coremodule_notloaded_b")) { HeightExtra = 0f, TextColor = Color.OrangeRed });

                        foreach (Tuple<EverestModuleMetadata, Action> mod in Everest.Loader.Delayed) {
                            string missingDepsString = "";
                            if(mod.Item1.Dependencies != null) {
                                // check for missing dependencies
                                List<EverestModuleMetadata> missingDependencies = mod.Item1.Dependencies
                                    .FindAll(dep => !Everest.Loader.DependencyLoaded(dep));

                                if(missingDependencies.Count != 0) {
                                    // format their names and versions, and join all of them in a single string
                                    missingDepsString = string.Join(", ", missingDependencies.Select(dependency => dependency.Name + " | v." + dependency.VersionString));

                                    // ensure that string is not too long, or else it would break the display
                                    if (missingDepsString.Length > 40) {
                                        missingDepsString = missingDepsString.Substring(0, 40) + "...";
                                    }

                                    // wrap that in a " ({list} not found)" message
                                    missingDepsString = $" ({missingDepsString} {Dialog.Clean("modoptions_coremodule_notloaded_notfound")})";
                                }
                            }

                            menu.Add(new TextMenuExt.SubHeaderExt(mod.Item1.Name + " | v." + mod.Item1.VersionString + missingDepsString) {
                                HeightExtra = 0f, TextColor = Color.PaleVioletRed
                            });
                        }
                    }
                }

                if (Everest.Updater.HasUpdate) {
                    menu.Add(new TextMenu.Button(Dialog.Clean("modoptions_coremodule_update").Replace("((version))", Everest.Updater.Newest.Build.ToString())).Pressed(() => {
                        Everest.Updater.Update(OuiModOptions.Instance.Overworld.Goto<OuiLoggedProgress>());
                    }));
                }
            }

            base.CreateModMenuSection(menu, inGame, snapshot);

            if (!inGame) {
                menu.Add(new TextMenu.Button(Dialog.Clean("modoptions_coremodule_soundtest")).Pressed(() => {
                    OuiModOptions.Instance.Overworld.Goto<OuiSoundTest>();
                }));

                menu.Add(new TextMenu.Button(Dialog.Clean("modoptions_coremodule_versionlist")).Pressed(() => {
                    OuiModOptions.Instance.Overworld.Goto<OuiVersionList>();
                }));

                menu.Add(new TextMenu.Button(Dialog.Clean("modoptions_coremodule_modupdates")).Pressed(() => {
                    OuiModOptions.Instance.Overworld.Goto<OuiModUpdateList>();
                }));
            }
        }

    }
}
