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

        private static Point VectorToPoint(Vector2 source) {
            return new(
                (int) Math.Round(source.X, MidpointRounding.ToEven),
                (int) Math.Round(source.Y, MidpointRounding.ToEven)
            );
        }

        public bool Move(Vector2 move, Collision onCollide = null, Solid pusher = null) {
            movementCounter += move;
            Point num = VectorToPoint(move);
            if (num != Point.Zero) {
                movementCounter.X -= num.X;
                movementCounter.Y -= num.Y;
                return MoveExact(num, onCollide, pusher);
            }
            return false;
        }

        public bool MoveExact(Vector2 move, Collision onCollide = null, Solid pusher = null)
            => MoveExact(VectorToPoint(move), onCollide, pusher);

        public bool MoveExact(Point move, Collision onCollide = null, Solid pusher = null) {
            Vector2 targetPosition = Position + Vector2.UnitX * move.X + Vector2.UnitY * move.Y;
            Point num = new(Math.Sign(move.X), Math.Sign(move.Y));
            Point num2 = new(0, 0);
            while (move.X != 0 && move.Y != 0) {
                Platform platform = CollideFirst<Solid>(Position + Vector2.UnitX * num.X + Vector2.UnitY * num.Y);
                if (platform != null) {
                    movementCounter = Vector2.Zero;
                    onCollide?.Invoke(new CollisionData {
                        Direction = Vector2.UnitX * num.X + Vector2.UnitY * num.Y,
                        Moved = Vector2.UnitX * num2.X + Vector2.UnitY * num2.Y,
                        TargetPosition = targetPosition,
                        Hit = platform,
                        Pusher = pusher
                    });
                    return true;
                }
                if (move.Y > 0 && !IgnoreJumpThrus) {
                    platform = CollideFirstOutside<JumpThru>(Position + Vector2.UnitX * num.X + Vector2.UnitY * num.Y);
                    if (platform != null) {
                        movementCounter = Vector2.Zero;
                        onCollide?.Invoke(new CollisionData {
                            Direction = Vector2.UnitX * num.X + Vector2.UnitY * num.Y,
                            Moved = Vector2.UnitX * num2.X + Vector2.UnitY * num2.Y,
                            TargetPosition = targetPosition,
                            Hit = platform,
                            Pusher = pusher
                        });
                        return true;
                    }
                }
                num2.X += num.X;
                num2.Y += num.Y;
                move.X -= num.X;
                move.Y -= num.Y;
                base.X += num.X;
                base.Y += num.Y;
            }
            return false;
        }

        public void MoveTowards(Vector2 target, Vector2 maxAmount, Collision onCollide = null) {
            Vector2 to = patch_Calc.Approach(ExactPosition, target, maxAmount);
            MoveTo(to, onCollide);
        }

        public void MoveTowards(Vector2 target, float maxAmount, Collision onCollide = null) {
            Vector2 to = patch_Calc.Approach(ExactPosition, target, maxAmount);
            MoveTo(to, onCollide);
        }

        public void MoveTo(Vector2 to, Collision onCollide = null) {
            Move(to - Position - movementCounter, onCollide);
        }
    }
}
