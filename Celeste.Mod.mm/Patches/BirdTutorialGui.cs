#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections.Generic;

namespace Celeste {
    class patch_BirdTutorialGui : BirdTutorialGui {
        public patch_BirdTutorialGui(Entity entity, Vector2 position, object info, params object[] controls)
            : base(entity, position, info, controls) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // On version 1.3.3.0+ (that have the ButtonPrompt enum instead of VirtualButton), we want to patch in some code to maintain compatibility with VirtualButtons.

        [MonoModIfFlag("Has:BirdTutorialGuiButtonPromptEnum")]
        private static Dictionary<VirtualButton, ButtonPrompt> controlsButtonsToID;
        [MonoModIfFlag("Has:BirdTutorialGuiButtonPromptEnum")]
        private static Dictionary<ButtonPrompt, VirtualButton> controlsIDToButton;
        [MonoModIfFlag("Has:BirdTutorialGuiButtonPromptEnum")]
        static patch_BirdTutorialGui() {
            controlsButtonsToID = new Dictionary<VirtualButton, ButtonPrompt>();
            controlsIDToButton = new Dictionary<ButtonPrompt, VirtualButton>();
        }

        public extern void orig_ctor(Entity entity, Vector2 position, object info, params object[] controls);
        [MonoModConstructor]
        [MonoModIfFlag("Has:BirdTutorialGuiButtonPromptEnum")]
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

        [MonoModIfFlag("Has:BirdTutorialGuiButtonPromptEnum")]
        public static new VirtualButton ButtonPromptToVirtualButton(ButtonPrompt prompt) {
            if (controlsIDToButton.TryGetValue(prompt, out VirtualButton btn))
                return btn;
            return orig_ButtonPromptToVirtualButton(prompt);
        }
    }
}
