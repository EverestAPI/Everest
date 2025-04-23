using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using MonoMod;

namespace Celeste {
    class patch_OuiAssistMode : OuiAssistMode {

        private float fade;
        
        public patch_OuiAssistMode() 
            : base() {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModReplace]
        private new Color SelectionColor(bool selected) {
            if (selected) {
                return ((!CoreModule.Settings.AllowTextHighlight || base.Scene.BetweenInterval(0.1f)) ? TextMenu.HighlightColorA : TextMenu.HighlightColorB) * fade;
            }
            return Color.White * fade;
        }
    }
}
