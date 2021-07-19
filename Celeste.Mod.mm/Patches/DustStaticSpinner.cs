using Microsoft.Xna.Framework;
using MonoMod;

namespace Celeste {
    class patch_DustStaticSpinner : DustStaticSpinner {

        public patch_DustStaticSpinner(Vector2 position, bool attachToSolid, bool ignoreSolids = false)
            : base(position, attachToSolid, ignoreSolids) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModReplace]
        private void OnShake(Vector2 amount) {
            // fix from vanilla: add to the position instead of replacing it (that's what expected from OnShake in static movers).
            Sprite.Position += amount;
        }
    }
}
