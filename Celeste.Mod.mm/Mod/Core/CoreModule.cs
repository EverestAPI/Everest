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

namespace Celeste.Mod.Core {
    /// <summary>
    /// The Everest core module class. Feel free to access the core module settings from your own mod.
    /// </summary>
    public class CoreModule : EverestModule {

        public static CoreModule Instance;

        public override Type SettingsType => typeof(CoreModuleSettings);
        public static CoreModuleSettings Settings => (CoreModuleSettings) Instance._Settings;

        public override Type SaveDataType => typeof(CoreModuleSaveData);
        public static CoreModuleSaveData SaveData => (CoreModuleSaveData) Instance._SaveData;

        public CoreModule() {
            Instance = this;

            // Runtime modules shouldn't do this.
            Metadata = new EverestModuleMetadata() {
                Name = "Everest",
                Version = Everest.Version
            };
        }

        public override void LoadSettings() {
            base.LoadSettings();

            // If we're running in an environment that prefers those flag, forcibly enable them.
            Settings.LazyLoading |= Everest.Flags.PreferLazyLoading;
            Settings.LQAtlas |= Everest.Flags.PreferLQAtlas;

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
            Everest.Events.OuiMainMenu.OnCreateButtons += CreateMainMenuButtons;
            Everest.Events.Level.OnCreatePauseMenuButtons += CreatePauseMenuButtons;

            if (Everest.Flags.IsMobile) {
                // It shouldn't look that bad on mobile screens...
                Environment.SetEnvironmentVariable("FNA_OPENGL_BACKBUFFER_SCALE_NEAREST", "1");
            }
        }

        public override void Initialize() {
            // F5 - Reload and restart the current screen.
            Engine.Commands.FunctionKeyActions[4] = () => {
                Level level = Engine.Scene as Level;
                if (level == null)
                    return;
                AreaData.Areas[level.Session.Area.ID].Mode[(int) level.Session.Area.Mode].MapData.Reload();
                Engine.Scene = new LevelLoader(new Session(level.Session.Area, null, null) {
                    FirstLevel = false,
                    Level = level.Session.Level,
                    StartedFromBeginning = level.Session.StartedFromBeginning
                }, level.Session.RespawnPoint);
            };

            // F6 - Open map editor for current level.
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

            if (GFX.MountainTerrain == null && Settings.NonThreadedGL) {
                GFX.MountainTerrain = ObjModel.Create(Path.Combine(Engine.ContentDirectory, "Overworld", "mountain.obj"));
                GFX.MountainBuildings = ObjModel.Create(Path.Combine(Engine.ContentDirectory, "Overworld", "buildings.obj"));
                GFX.MountainCoreWall = ObjModel.Create(Path.Combine(Engine.ContentDirectory, "Overworld", "mountain_wall.obj"));
            }
            // Otherwise loaded in GameLoader.LoadThread
        }

        public override void Unload() {
            Everest.Events.OuiMainMenu.OnCreateButtons -= CreateMainMenuButtons;
            Everest.Events.Level.OnCreatePauseMenuButtons -= CreatePauseMenuButtons;

        }

        public void CreateMainMenuButtons(OuiMainMenu menu, List<MenuButton> buttons) {
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

            buttons.Insert(index, new MainMenuSmallButton("menu_modoptions", "menu/modoptions", menu, Vector2.Zero, Vector2.Zero, () => {
                Audio.Play("event:/ui/main/button_select");
                Audio.Play("event:/ui/main/whoosh_large_in");
                menu.Overworld.Goto<OuiModOptions>();
            }));
        }

        public void CreatePauseMenuButtons(Level level, TextMenu menu, bool minimal) {
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
                
                level.Paused = true;

			    TextMenu options = OuiModOptions.CreateMenu(true, LevelExt.PauseSnapshot);

			    options.OnESC = options.OnCancel = () => {
				    Audio.Play("event:/ui/main/button_back");
				    options.CloseAndRun(Everest.SaveSettings(), () => level.Pause(returnIndex, minimal, false));
			    };

			    options.OnPause = () => {
				    Audio.Play("event:/ui/main/button_back");
				    options.CloseAndRun(Everest.SaveSettings(), () => {
                        level.Paused = false;
                        Engine.FreezeTimer = 0.15f;
                    });
			    };

			    level.Add(options);
            }));
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            if (!inGame) {
                if (Everest.Updater.HasUpdate) {
                    menu.Add(new TextMenu.Button(Dialog.Clean("modoptions_coremodule_update").Replace("((version))", Everest.Updater.Newest.Version.ToString())).Pressed(() => {
                        Everest.Updater.Update(OuiModOptions.Instance.Overworld.Goto<OuiLoggedProgress>());
                    }));
                }

                // Allow downgrading travis / dev builds.
                if (Celeste.PlayMode == Celeste.PlayModes.Debug || Everest.VersionSuffix.StartsWith("travis-") || Everest.VersionSuffix == "dev") {
                    menu.Add(new TextMenu.Button(Dialog.Clean("modoptions_coremodule_versionlist")).Pressed(() => {
                        OuiModOptions.Instance.Overworld.Goto<OuiVersionList>();
                    }));
                }
            }

            base.CreateModMenuSection(menu, inGame, snapshot);

            if (Celeste.PlayMode == Celeste.PlayModes.Debug && Engine.Instance.Version >= new Version(1, 2, 2, 4)) {
                menu.Add(new TextMenu.Button(Dialog.Clean("modoptions_coremodule_unlocksecretchar")).Pressed(() => {
                    Audio.Play("event:/char/dialogue/secret_character");
                }));
            }

        }

    }
}
