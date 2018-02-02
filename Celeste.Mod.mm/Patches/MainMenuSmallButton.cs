#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    class patch_MainMenuSmallButton : MainMenuSmallButton {

        public string LabelName;
        public string IconName;

        public patch_MainMenuSmallButton(string labelName, string iconName, Oui oui, Vector2 targetPosition, Vector2 tweenFrom, Action onConfirm)
            : base(labelName, iconName, oui, targetPosition, tweenFrom, onConfirm) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // Patching constructors is ugly.
        public extern void orig_ctor_MainMenuSmallButton(string labelName, string iconName, Oui oui, Vector2 targetPosition, Vector2 tweenFrom, Action onConfirm);
        [MonoModConstructor]
        public void ctor_MainMenuSmallButton(string labelName, string iconName, Oui oui, Vector2 targetPosition, Vector2 tweenFrom, Action onConfirm) {
            LabelName = labelName;
            IconName = iconName;
            orig_ctor_MainMenuSmallButton(labelName, iconName, oui, targetPosition, tweenFrom, onConfirm);
        }

    }
    public static class MainMenuSmallButtonExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static string GetLabelName(this MainMenuSmallButton self)
            => ((patch_MainMenuSmallButton) self).LabelName;

        public static string GetIconName(this MainMenuSmallButton self)
            => ((patch_MainMenuSmallButton) self).IconName;

    }
}
