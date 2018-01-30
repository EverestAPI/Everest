using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    class CoreEverestModule : EverestModule {

        public override void CreateMainMenuButtons(OuiMainMenu menu, List<MenuButton> buttons) {
            buttons.Add(new MainMenuSmallButton("menu_test", "menu/options", menu, Vector2.Zero, Vector2.Zero, () => {
                Console.WriteLine("Hello, World!");
            }));
        }

    }
}
