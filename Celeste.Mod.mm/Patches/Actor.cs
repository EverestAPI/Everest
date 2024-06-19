#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using MonoMod;

namespace Celeste {
    class patch_Actor : Actor {

        private Vector2 movementCounter = default;

        public patch_Actor(Vector2 position)
            : base(position) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // Legacy Support
        protected bool TrySquishWiggle(CollisionData data) {
            return TrySquishWiggle(data, 3, 3);
        }

        // Patch MoveToX/Y to replicate XNA's behaviour on FNA

        [MonoModReplace]
        public new void MoveToX(float toX, Collision onCollide = null) {
            MoveH((float) ((double) toX - Position.X - movementCounter.X), onCollide);
        }

        [MonoModReplace]
        public new void MoveToY(float toY, Collision onCollide = null) {
            MoveV((float) ((double) toY - Position.Y - movementCounter.Y), onCollide);
        }

    }
}
