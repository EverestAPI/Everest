#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0108 // Method hides inherited member

using Celeste.Mod;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

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

    }
}
