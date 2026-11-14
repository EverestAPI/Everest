using Celeste.Mod.Core;
using Microsoft.Xna.Framework;

namespace Celeste {
    public class patch_Booster : Booster {

        private bool red;
        
        public patch_Booster(Vector2 position, bool red)
            : base(position, red) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_Render();
        public override void Render() {
            orig_Render();
            if (CoreModule.Settings.ColourblindnessAccessibility)
                GFX.Game["everest/objects/booster" + (red ? "Red" : "") + "/colorblindness_symbol"].DrawCentered(Position);
        }
        
    }
}
