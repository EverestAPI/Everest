using Microsoft.Xna.Framework;
using System;

namespace Celeste {
    abstract class patch_MenuButton : MenuButton {

        private bool selected;
        public bool _Selected {
            get => selected;
            set => selected = value;
        }

        public patch_MenuButton(Oui oui, Vector2 targetPosition, Vector2 tweenFrom, Action onConfirm)
            : base(oui, targetPosition, tweenFrom, onConfirm) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }
    }

    public static partial class MenuButtonExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Set the button's inner selected value.
        /// </summary>
        public static void SetSelected(this MenuButton self, bool value)
            => ((patch_MenuButton) self)._Selected = value;

    }
}