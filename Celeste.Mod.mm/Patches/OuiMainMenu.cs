#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_OuiMainMenu : OuiMainMenu {

        // We're effectively in OuiTitleScreen, but still need to "expose" private fields to our mod.
        private List<MenuButton> buttons;
        public List<MenuButton> Buttons => buttons;
        private MainMenuClimb climbButton;

        public extern void orig_CreateButtons();
        public new void CreateButtons() {
            orig_CreateButtons();

            Everest.Events.MainMenu.CreateButtons(this, buttons);

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

                button.UpButton = i > 0 ? buttons[i - 1] : buttons[buttons.Count - 1];
                button.DownButton = i < buttons.Count - 1 ? buttons[i + 1] : buttons[0];

                // Add button if missing.
                if (!Scene.Entities.Contains(button))
                    Scene.Add(button);
            }
        }

        [MonoModReplace]
        private void OnDebug() {
            Audio.Play("event:/ui/main/whoosh_list_out");
            Audio.Play("event:/ui/main/button_select");

            SaveData.InitializeDebugMode(true);

            if (SaveData.Instance.CurrentSession != null && SaveData.Instance.CurrentSession.InArea) {
                Audio.SetMusic(null);
                Audio.SetAmbience(null);
                Overworld.ShowInputUI = false;
                new FadeWipe(Scene, false, () => LevelEnter.Go(SaveData.Instance.CurrentSession, true));
                return;
            }

            Overworld.Goto<OuiChapterSelect>();
        }

    }
    public static class OuiMainMenuExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static List<MenuButton> GetButtons(this OuiMainMenu self)
            => ((patch_OuiMainMenu) self).Buttons;

    }
}
