#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0108 // Method hides inherited member

using MonoMod;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Linq;
using Microsoft.Xna.Framework.Input;

namespace Monocle {
    class patch_Binding : Binding {

        // Serialized in Everest modsettings to avoid issues when switching to vanilla
        [XmlIgnore]
        public List<patch_MInput.patch_MouseData.MouseButtons> Mouse;

        public extern void orig_ctor();
        [MonoModConstructor]
        public void ctor() {
            orig_ctor();
            Mouse = new List<patch_MInput.patch_MouseData.MouseButtons>();
        }

        [MonoModReplace]
        public bool get_HasInput() {
            return Keyboard.Count > 0 || Controller.Count > 0 || Mouse.Count > 0;
        }

        public bool Add(params patch_MInput.patch_MouseData.MouseButtons[] buttons) {
            bool result = false;
            foreach (patch_MInput.patch_MouseData.MouseButtons button in buttons) {
                if (!Mouse.Contains(button) && ExclusiveFrom.TrueForAll(item => !((patch_Binding) item).Needs(button))) {
                    Mouse.Add(button);
                    result = true;
                }
            }
            return result;
        }

        public bool Needs(patch_MInput.patch_MouseData.MouseButtons button) {
            if (Mouse.Contains(button)) {
                // Keyboard takes priority
                if (Keyboard.Count + Mouse.Count <= 1)
                    return true;

                if (!IsExclusive(button))
                    return false;

                foreach (patch_MInput.patch_MouseData.MouseButtons item in Mouse) {
                    if (item != button && IsExclusive(item)) {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        public bool IsExclusive(patch_MInput.patch_MouseData.MouseButtons button) {
            foreach (patch_Binding item in ExclusiveFrom) {
                if (item.Mouse.Contains(button)) {
                    return false;
                }
            }
            return true;
        }

        public bool ClearMouse() {
            int items = Mouse.Count;
            Mouse.Clear();
            return items > 0;
        }

        public extern float orig_Axis(int gamepadIndex, float threshold);
        public float Axis(int gamepadIndex, float threshold) {
            float axis = orig_Axis(gamepadIndex, threshold);
            if (axis == 0f) {
                for (int i = 0; i < Mouse.Count; i++) {
                    if (patch_MInput.Mouse.Check(Mouse[i])) {
                        return 1f;
                    }
                }
            }

            return axis;
        }

        public extern bool orig_Check(int gamepadIndex, float threshold);
        public bool Check(int gamepadIndex, float threshold) {
            if (orig_Check(gamepadIndex, threshold))
                return true;

            for (int i = 0; i < Mouse.Count; i++) {
                if (patch_MInput.Mouse.Check(Mouse[i])) {
                    return true;
                }
            }

            return false;
        }

        public extern bool orig_Pressed(int gamepadIndex, float threshold);
        public bool Pressed(int gamepadIndex, float threshold) {
            if (orig_Pressed(gamepadIndex, threshold))
                return true;

            for (int i = 0; i < Mouse.Count; i++) {
                if (patch_MInput.Mouse.Pressed(Mouse[i])) {
                    return true;
                }
            }

            return false;
        }

        public extern bool orig_Released(int gamepadIndex, float threshold);
        public bool Released(int gamepadIndex, float threshold) {
            if (orig_Released(gamepadIndex, threshold))
                return true;

            for (int i = 0; i < Mouse.Count; i++) {
                if (patch_MInput.Mouse.Released(Mouse[i])) {
                    return true;
                }
            }

            return false;
        }

        public bool Remove(params Keys[] keys) {
            // use a stupid way: pretend the keys are removed and check if the rest are still valid
            List<Keys> before = new List<Keys>(Keyboard);
            List<Keys> after = Keyboard.Where(key => !keys.Contains(key)).ToList();
            if (after.Count == 0 && ExclusiveFrom.Count > 0) {
                return false;
            }

            // assign to field is necessary because it's accessed when cross references exist
            Keyboard = after;
            bool result = true;
            foreach (Keys key in after) {
                if (ExclusiveFrom.Any(b => b.Needs(key))) {
                    result = false;
                    break;
                }
            }
            Keyboard = result ? after : before;

            return result;
        }

        public bool Remove(params Buttons[] buttons) {
            // use a stupid way: pretend the buttons are removed and check if the rest are still valid
            List<Buttons> before = new List<Buttons>(Controller);
            List<Buttons> after = Controller.Where(button => !buttons.Contains(button)).ToList();
            if (after.Count == 0 && ExclusiveFrom.Count > 0) {
                return false;
            }

            // assign to field is necessary because it's accessed when cross references exist
            Controller = after;
            bool result = true;
            foreach (Buttons button in after) {
                if (ExclusiveFrom.Any(b => b.Needs(button))) {
                    result = false;
                    break;
                }
            }
            Controller = result ? after : before;

            return result;
        }

    }
}
