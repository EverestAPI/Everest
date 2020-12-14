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

        // Celeste 1.3.3.0 comes with a new parameter.

        /*
        [MonoModLinkTo("Celeste.Input", "System.Boolean GuiInputController()")]
        [MonoModIgnore]
        public static extern bool GuiInputControllerOld();
        */

        [MonoModIfFlag("V1:GuiInputController")]
        public static bool GuiInputController(PrefixMode mode = PrefixMode.Latest) {
            return GuiInputController();
        }

        [MonoModIfFlag("V2:GuiInputController")]
        public static bool GuiInputController() {
            return GuiInputController(PrefixMode.Latest);
        }

        [MonoModIfFlag("V1:GuiInputController")]
        public static MTexture GuiButton(VirtualButton button, PrefixMode mode = PrefixMode.Latest, string fallback = "controls/keyboard/oemquestion") {
            return GuiButton(button, fallback);
        }

        [MonoModIfFlag("V2:GuiInputController")]
        public static MTexture GuiButton(VirtualButton button, string fallback = "controls/keyboard/oemquestion") {
            return GuiButton(button, PrefixMode.Latest, fallback);
        }

        [MonoModIfFlag("V1:GuiInputController")]
        public static MTexture GuiSingleButton(Buttons button, PrefixMode mode = PrefixMode.Latest, string fallback = "controls/keyboard/oemquestion") {
            return GuiSingleButton(button, fallback);
        }

        [MonoModIfFlag("V2:GuiInputController")]
        public static MTexture GuiSingleButton(Buttons button, string fallback = "controls/keyboard/oemquestion") {
            return GuiSingleButton(button, PrefixMode.Latest, fallback);
        }

        [MonoModIfFlag("V1:EaseCamera")]
        public enum PrefixMode {
            Latest,
            Attached
        }

    }
}
