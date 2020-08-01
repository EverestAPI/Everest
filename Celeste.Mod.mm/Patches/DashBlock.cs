using Microsoft.Xna.Framework;

namespace Celeste {
    class patch_DashBlock : DashBlock {

        public patch_DashBlock(Vector2 position, char tiletype, float width, float height, bool blendIn, bool permanent, bool canDash, EntityID id)
            : base(position, tiletype, width, height, blendIn, permanent, canDash, id) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public void Break(Vector2 from, Vector2 direction, bool playSound = true) {
            Break(from, direction, playSound, true);
        }

    }
}
