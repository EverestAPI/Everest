using Celeste.Editor;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
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

        public override void Load() {
            Everest.Events.OuiMainMenu.OnCreateButtons += CreateMainMenuButtons;
            Everest.Events.Level.OnCreatePauseMenuButtons += CreatePauseMenuButtons;
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
            base.CreateModMenuSection(menu, inGame, snapshot);

            menu.Add(new TextMenu.Button(Dialog.Clean("modoptions_coremodule_recrawl")).Pressed(() => {
                Everest.Content.Recrawl();
                Everest.Content.Reprocess();
                VirtualContentExt.ForceReload();
                AreaData.Load();
            }));
        }

    }
}
