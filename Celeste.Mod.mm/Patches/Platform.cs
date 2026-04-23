using Microsoft.Xna.Framework;
using System;

namespace Celeste {
    abstract class patch_Platform : Platform {

        public patch_Platform(Vector2 position, bool safe)
            : base(position, safe) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public bool MoveVCollideSolidsAndBounds(Level level, float moveV, bool thruDashBlocks, Action<Vector2, Vector2, Platform> onCollide = null) {
            return MoveVCollideSolidsAndBounds(level, moveV, thruDashBlocks, onCollide, true);
        }

    }
}
