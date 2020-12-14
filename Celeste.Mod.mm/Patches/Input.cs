#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;

namespace Celeste {
    static class patch_Input {

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

            foreach (EverestModule mod in Everest._Modules)
                mod.OnInputDeregister();

            Everest.Events.Input.Deregister();
        }

        // ==== Methods modified in 1.3.3.0

        [MonoModIfFlag("Version:1330AndLater")]
        public static bool GuiInputController() {
            return Input.GuiInputController(Input.PrefixMode.Latest);
        }

        [MonoModIfFlag("Version:1330AndLater")]
        public static MTexture GuiButton(VirtualButton button, string fallback = "controls/keyboard/oemquestion") {
            return Input.GuiButton(button, Input.PrefixMode.Latest, fallback);
        }

        [MonoModIfFlag("Version:1330AndLater")]
        public static MTexture GuiSingleButton(Buttons button, string fallback = "controls/keyboard/oemquestion") {
            return Input.GuiSingleButton(button, Input.PrefixMode.Latest, fallback);
        }
    }
}
