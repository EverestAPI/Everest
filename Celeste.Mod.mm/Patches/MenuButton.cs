#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

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
using System.Xml;

namespace Celeste {
    abstract class patch_MenuButton : MenuButton {

        private bool selected;
        private Tween tween;
        public bool _Selected {
            get => selected;
            set => selected = value;
        }

        public patch_MenuButton(Oui oui, Vector2 targetPosition, Vector2 tweenFrom, Action onConfirm)
            : base(oui, targetPosition, tweenFrom, onConfirm) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }
        public extern void orig_TweenOut(float time);
        public extern void orig_TweenIn(float time);

        public new void TweenIn(float time) {
            orig_TweenIn(time);

            tween.OnComplete = t => {
                t.RemoveSelf();
                tween = null;
            };
        }

        public new void TweenOut(float time) {
            orig_TweenOut(time);

            tween.OnComplete = t => {
                t.RemoveSelf();
                tween = null;
            };
        }
    }

    public static partial class MenuButtonExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Set the button's inner selectted value.
        /// </summary>
        public static void SetSelected(this MenuButton self, bool value)
            => ((patch_MenuButton) self)._Selected = value;

    }
}