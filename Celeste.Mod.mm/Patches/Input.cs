#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;

namespace Celeste {
    static class patch_Input {

        // Celeste 1.3.3.X introduced DemoDash, 1.3.3.19 renamed it to CrouchDash
        [MonoModLinkFrom("Monocle.VirtualButton Celeste.Input.DemoDash")]
        public static VirtualButton CrouchDash;
        public static VirtualJoystick Feather;

        public static extern void orig_Initialize();
        public static void Initialize() {
            orig_Initialize();

            foreach (EverestModule mod in Everest._Modules)
                mod.OnInputInitialize();

            Everest.Events.Input.Initialize();
        }

        public static extern void orig_Deregister();
        public static void Deregister() {
            orig_Deregister();
            Feather?.Deregister();

            foreach (EverestModule mod in Everest._Modules)
                mod.OnInputDeregister();

            Everest.Events.Input.Deregister();
        }

        #region Legacy Support

        public static bool GuiInputController() {
            return Input.GuiInputController(Input.PrefixMode.Latest);
        }

        public static MTexture GuiButton(VirtualButton button, string fallback = "controls/keyboard/oemquestion") {
            return Input.GuiButton(button, Input.PrefixMode.Latest, fallback);
        }

        public static MTexture GuiSingleButton(Buttons button, string fallback = "controls/keyboard/oemquestion") {
            return Input.GuiSingleButton(button, Input.PrefixMode.Latest, fallback);
        }

        #endregion

    }
}