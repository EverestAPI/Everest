#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;

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

        public void ZeroRemainder() {
            movementCounter = Vector2.Zero;
        }

        private static int RoundF(float value) {
            return (int) Math.Round(value, MidpointRounding.ToEven);
        }

        public bool Move(Vector2 move, Collision onCollide = null, Solid pusher = null)
            => Move(move.X, move.Y, onCollide, pusher);

        public bool Move(float moveH, float moveV, Collision onCollide = null, Solid pusher = null) {
            movementCounter.X += moveH;
            movementCounter.Y += moveV;
            int numH = RoundF(moveH);
            int numV = RoundF(moveV);
            if (numH != 0 && numV != 0) {
                movementCounter.X -= numH;
                movementCounter.Y -= numV;
                return MoveExact(numH, numV, onCollide, pusher);
            }
            return false;
        }

        public bool MoveExact(Vector2 move, Collision onCollide = null, Solid pusher = null)
            => MoveExact(RoundF(move.X), RoundF(move.Y), onCollide, pusher);

        public bool MoveExact(int moveH, int moveV, Collision onCollide = null, Solid pusher = null) {
            Vector2 targetPosition = Position + Vector2.UnitX * moveH + Vector2.UnitY * moveV;
            int numH = Math.Sign(moveH);
            int numV = Math.Sign(moveV);
            int num2H = 0;
            int num2V = 0;
            while (moveH != 0 && moveV != 0) {
                Platform platform = CollideFirst<Solid>(Position + Vector2.UnitX * numH + Vector2.UnitY * numV);
                if (platform != null) {
                    movementCounter = Vector2.Zero;
                    onCollide?.Invoke(new CollisionData {
                        Direction = Vector2.UnitX * numH + Vector2.UnitY * numV,
                        Moved = Vector2.UnitX * num2H + Vector2.UnitY * num2V,
                        TargetPosition = targetPosition,
                        Hit = platform,
                        Pusher = pusher
                    });
                    return true;
                }
                if (moveV > 0 && !IgnoreJumpThrus) {
                    platform = CollideFirstOutside<JumpThru>(Position + Vector2.UnitX * numH + Vector2.UnitY * numV);
                    if (platform != null) {
                        movementCounter = Vector2.Zero;
                        onCollide?.Invoke(new CollisionData {
                            Direction = Vector2.UnitX * numH + Vector2.UnitY * numV,
                            Moved = Vector2.UnitX * num2H + Vector2.UnitY * num2V,
                            TargetPosition = targetPosition,
                            Hit = platform,
                            Pusher = pusher
                        });
                        return true;
                    }
                }
                num2H += numH;
                num2V += numV;
                moveH -= numH;
                moveV -= numV;
                base.X += numH;
                base.Y += numV;
            }
            return false;
        }

        public void MoveTowards(Vector2 target, float maxAmount, Collision onCollide = null)
            => MoveTowards(target.X, target.Y, maxAmount, onCollide);

        public void MoveTowards(Vector2 target, Vector2 maxAmount, Collision onCollide = null)
            => MoveTowards(target.X, target.Y, maxAmount.X, maxAmount.Y, onCollide);

        public void MoveTowards(float targetX, float targetY, float maxAmount, Collision onCollide = null)
            => MoveTowards(targetX, targetY, maxAmount, maxAmount, onCollide);

        public void MoveTowards(float targetX, float targetY, float maxAmountX, float maxAmountY, Collision onCollide = null) {
            float toX = Calc.Approach(ExactPosition.X, targetX, maxAmountX);
            float toY = Calc.Approach(ExactPosition.Y, targetY, maxAmountY);
            MoveTo(toX, toY, onCollide);
        }

        public void MoveTo(Vector2 to, Collision onCollide = null)
            => MoveTo(to.X, to.Y, onCollide);

        public void MoveTo(float toX, float toY, Collision onCollide = null) {
            Move(toX, toY, onCollide);
        }
    }
}
