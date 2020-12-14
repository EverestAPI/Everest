#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections.Generic;

namespace Celeste {
    class patch_BirdTutorialGui : BirdTutorialGui {

        private static Dictionary<VirtualButton, ButtonPrompt> controlsButtonsToID = new Dictionary<VirtualButton, ButtonPrompt>();
        private static Dictionary<ButtonPrompt, VirtualButton> controlsIDToButton = new Dictionary<ButtonPrompt, VirtualButton>();

        public patch_BirdTutorialGui(Entity entity, Vector2 position, object info, params object[] controls)
            : base(entity, position, info, controls) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(Entity entity, Vector2 position, object info, params object[] controls);
        [MonoModConstructor]
        public void ctor(Entity entity, Vector2 position, object info, params object[] controls) {
            for (int i = 0; i < controls.Length; i++) {
                if (controls[i] is VirtualButton btn) {
                    if (!controlsButtonsToID.TryGetValue(btn, out ButtonPrompt id)) {
                        id = (ButtonPrompt) (-(controlsButtonsToID.Count + 1));
                        controlsButtonsToID[btn] = id;
                        controlsIDToButton[id] = btn;
                    }
                    controls[i] = id;
                }
            }
            orig_ctor(entity, position, info, controls);
        }

        public static extern VirtualButton orig_ButtonPromptToVirtualButton(ButtonPrompt prompt);
        public static new VirtualButton ButtonPromptToVirtualButton(ButtonPrompt prompt) {
            if (controlsIDToButton.TryGetValue(prompt, out VirtualButton btn))
                return btn;
            return orig_ButtonPromptToVirtualButton(prompt);
        }

    }
}
