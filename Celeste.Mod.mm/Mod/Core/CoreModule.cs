using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    class CoreModule : EverestModule {

        public CoreModule() {
            Metadata = new EverestModuleMetadata() {
                Name = "Everest",
                Version = Everest.Version
            };
        }

        public override void Load() {
            Everest.Events.OuiMainMenu.OnCreateMainMenuButtons += CreateMainMenuButtons;

        }

        public override void Unload() {
            Everest.Events.OuiMainMenu.OnCreateMainMenuButtons -= CreateMainMenuButtons;

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

    }
}
