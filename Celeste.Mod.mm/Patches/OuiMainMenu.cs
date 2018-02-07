#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword

using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_OuiMainMenu : OuiMainMenu {

        // We're effectively in OuiTitleScreen, but still need to "expose" private fields to our mod.
        private List<MenuButton> buttons;
        private MainMenuClimb climbButton;

        public extern void orig_CreateButtons();
        public void CreateButtons() {
            orig_CreateButtons();

            Everest.Events.OuiMainMenu.CreateButtons(this, buttons);

            // Current button position.
            Vector2 pos = new Vector2(320f, 160f);
            // Static offset which is always added.
            Vector2 offs = new Vector2(-640f, 0f);

            // Recalculate button positions and up / down.
            for (int i = 0; i < buttons.Count; i++) {
                MenuButton button = buttons[i];

                button.TargetPosition = pos;
                button.Position = button.TweenFrom = pos + offs;
                if (Visible && Focused)
                    button.Position = button.TargetPosition;

                pos += Vector2.UnitY * button.ButtonHeight;
                // Special case: Climb button changes pos horizontally.
                if (button == climbButton)
                    pos.X -= 140f;

                button.UpButton = i > 0 ? buttons[i - 1] : null;
                button.DownButton = i < buttons.Count - 1 ? buttons[i + 1] : null;

                // Add button if missing.
                if (!Scene.Entities.Contains(button))
                    Scene.Add(button);
            }
        }

    }
}
