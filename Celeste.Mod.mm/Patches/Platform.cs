using Microsoft.Xna.Framework;
using MonoMod;
using System;

namespace Celeste {
    abstract class patch_Platform : Platform {

        private Vector2 movementCounter = default;
        internal Vector2 _movementCounter => movementCounter; // proxy for Solid patches to link against

        public patch_Platform(Vector2 position, bool safe)
            : base(position, safe) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public bool MoveVCollideSolidsAndBounds(Level level, float moveV, bool thruDashBlocks, Action<Vector2, Vector2, Platform> onCollide = null) {
            return MoveVCollideSolidsAndBounds(level, moveV, thruDashBlocks, onCollide, true);
        }

        // Patch MoveToX/Y to replicate XNA's behaviour on FNA

        [MonoModReplace]
        public new void MoveToX(float toX) {
            MoveH((float) ((double) toX - Position.X - movementCounter.X));
        }

        [MonoModReplace]
        public new void MoveToX(float toX, float liftSpeedX) {
            MoveH((float) ((double) toX - Position.X - movementCounter.X), liftSpeedX);
        }

        [MonoModReplace]
        public new void MoveToXNaive(float toX) {
            MoveHNaive((float) ((double) toX - Position.X - movementCounter.X));
        }

        [MonoModReplace]
        public new void MoveToY(float toY) {
            MoveV((float) ((double) toY - Position.Y - movementCounter.Y));
        }

        [MonoModReplace]
        public new void MoveToY(float toY, float liftSpeedY) {
            MoveV((float) ((double) toY - Position.Y - movementCounter.Y), liftSpeedY);
        }

        [MonoModReplace]
        public new void MoveToYNaive(float toY) {
            MoveVNaive((float) ((double) toY - Position.Y - movementCounter.Y));
        }

    }
}
