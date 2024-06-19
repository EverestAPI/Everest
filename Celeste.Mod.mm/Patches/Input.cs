#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;

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

            //Sets the slight camera movement on the map to the set debug camera movement keys in Everest mod settings
            Input.MountainAim = new VirtualJoystick(
                CoreModule.Settings.CameraForward.Binding,
                CoreModule.Settings.CameraBackward.Binding,
                CoreModule.Settings.CameraLeft.Binding,
                CoreModule.Settings.CameraRight.Binding,
                Input.Gamepad, 0.1f
            );

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

        [MonoModIgnore]
        private static extern MTexture GuiTexture(string prefix, string input);

        [MonoModIgnore]
        public static extern MTexture orig_GuiButton(VirtualButton button, Input.PrefixMode mode = Input.PrefixMode.Latest, string fallback = "controls/keyboard/oemquestion");

        public static MTexture GuiButton(VirtualButton button, Input.PrefixMode mode = Input.PrefixMode.Latest, string fallback = "controls/keyboard/oemquestion") {
            if (!GuiInputController() && Input.FirstKey(button) == Keys.None) {
                foreach (patch_MInput.patch_MouseData.MouseButtons mouseBtn in ((patch_Binding)button.Binding).Mouse)
                    return GuiMouseButton(mouseBtn, mode, fallback);
            }
            return orig_GuiButton(button, mode, fallback);
        }

        public static MTexture GuiMouseButton(patch_MInput.patch_MouseData.MouseButtons button, Input.PrefixMode mode = Input.PrefixMode.Latest, string fallback = "controls/keyboard/oemquestion") {
            // GuiKey uses a keyNameLookup to cache the Key: string values, but implementing one here would also require initializing it somewhere.
            string name = button.ToString();
            MTexture mTexture = GuiTexture("mouse", name);
            if (mTexture is null && fallback is not null)
                return GFX.Gui[fallback];
            return mTexture;
        }


        [MonoModReplace]
        [MonoModIfFlag("RelinkXNA")]
        public static string GuiInputPrefix(Input.PrefixMode mode = Input.PrefixMode.Latest) {
            if (!string.IsNullOrEmpty(Input.OverrideInputPrefix))
                return Input.OverrideInputPrefix;

            // There's an unused boolean flag in for FNA here, but the compiler would just optimize it away

            if ((mode != Input.PrefixMode.Latest) ? MInput.GamePads[Input.Gamepad].Attached : MInput.ControllerHasFocus) {
                string guid = GamePad.GetGUIDEXT(MInput.GamePads[Input.Gamepad].PlayerIndex);
                if (guid.Equals("4c05c405") || guid.Equals("4c05cc09"))
                    return "ps4";
                if (guid.Equals("7e050920") || guid.Equals("7e053003"))
                    return "ns";
                if (guid.Equals("d1180094"))
                    return "stadia";
                else
                    return "xb1";
            }

            return "keyboard";
        }

        [MonoModReplace]
        [MonoModIfFlag("RelinkXNA")]
        public static void SetLightbarColor(Color color) {
            color.R = (byte) (Math.Pow(color.R / 255f, 3) * 255.0);
            color.G = (byte) (Math.Pow(color.G / 255f, 3) * 255.0);
            color.B = (byte) (Math.Pow(color.B / 255f, 3) * 255.0);
            GamePad.SetLightBarEXT((PlayerIndex) Input.Gamepad, color);
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