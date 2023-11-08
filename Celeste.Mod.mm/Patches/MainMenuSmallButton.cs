#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // field is never assigned to, and will always have its default value

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;

namespace Celeste {
    class patch_MainMenuSmallButton : MainMenuSmallButton {

        private float ease;
        private Wiggler wiggler;

        // expose these fields to extending classes.
        public float Ease => ease;
        public Wiggler Wiggler => wiggler;

        /// <summary>
        /// The original label name dialog key.<br/>
        /// Useful when inserting your own button between others.
        /// </summary>
        public string LabelName;
        /// <summary>
        /// The original GUI atlas icon path.<br/>
        /// Useful when inserting your own button between others.
        /// </summary>
        public string IconName;

        public patch_MainMenuSmallButton(string labelName, string iconName, Oui oui, Vector2 targetPosition, Vector2 tweenFrom, Action onConfirm)
            : base(labelName, iconName, oui, targetPosition, tweenFrom, onConfirm) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // Patching constructors is ugly.
        public extern void orig_ctor(string labelName, string iconName, Oui oui, Vector2 targetPosition, Vector2 tweenFrom, Action onConfirm);
        [MonoModConstructor]
        public void ctor(string labelName, string iconName, Oui oui, Vector2 targetPosition, Vector2 tweenFrom, Action onConfirm) {
            LabelName = labelName;
            IconName = iconName;
            orig_ctor(labelName, iconName, oui, targetPosition, tweenFrom, onConfirm);
        }

    }
    public static class MainMenuSmallButtonExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Get the original label name dialog key. Useful when inserting your own button between others.
        /// </summary>
        [Obsolete("Use MainMenuSmallButton.LabelName instead.")]
        public static string GetLabelName(this MainMenuSmallButton self)
            => ((patch_MainMenuSmallButton) self).LabelName;

        /// <summary>
        /// Get the original GUI atlas icon path. Useful when inserting your own button between others.
        /// </summary>
        [Obsolete("Use MainMenuSmallButton.IconName instead.")]
        public static string GetIconName(this MainMenuSmallButton self)
            => ((patch_MainMenuSmallButton) self).IconName;

        [Obsolete("Use MainMenuSmallButton.Ease instead.")]
        public static float GetEase(this MainMenuSmallButton self) {
            return ((patch_MainMenuSmallButton) self).Ease;
        }
        [Obsolete("Use MainMenuSmallButton.Wiggler instead.")]
        public static Wiggler GetWiggler(this MainMenuSmallButton self) {
            return ((patch_MainMenuSmallButton) self).Wiggler;
        }

    }
}
