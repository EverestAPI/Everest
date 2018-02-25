#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    static class patch_Input {

        public static extern void orig_Initialize();
        public static void Initialize() {
            orig_Initialize();
            Everest.Events.Input.Initialize();
        }

        public static extern void orig_Deregister();
        public static void Deregister() {
            orig_Deregister();
            Everest.Events.Input.Deregister();
        }

        public static string GuiInputPrefix() {
            if (!string.IsNullOrEmpty(Input.OverrideInputPrefix)) {
                return Input.OverrideInputPrefix;
            }
            if (!MInput.GamePads[Input.Gamepad].Attached) {
                return "keyboard";
            }
            string guid = GamePad.GetGUIDEXT(MInput.GamePads[Input.Gamepad].PlayerIndex);
            if (guid.Equals("4c05c405") || guid.Equals("4c05cc09") || guid.Equals("4c056802")) {
                return "ps4";
            }
            if (guid.Equals("7e050920") || guid.Equals("7e053003")) {
                return "ns";
            }
            return "xb1";
        }
    }
}
