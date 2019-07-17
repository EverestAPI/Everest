#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using System;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste {
    class patch_TheoCrystal : TheoCrystal {
        private Vector2 previousPosition;

        public patch_TheoCrystal(EntityData data, Vector2 offset)
            : base(data.Position + offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_HitSpinner(Entity spinner);

        public new void HitSpinner(Entity spinner) {
            if (Engine.Instance.Version > new Version(1, 2, 9, 1) || Everest.Flags.IsDisabled) {
                orig_HitSpinner(spinner);
                return;
            }

            // Quote from euni.
            // Floating point rounding works slightly differently in the XNA and OpenGL branches of Celeste.
            // We've known about this for a while, but thought it just caused minor desyncs depending on whether it was positive or negative. 
            // However, we recently found that it's responsible for a bug where Theo will move off of spikes in OpenGL
            // but not in XNA. More specifically, in TheoCrystal.HitSpinner, previousPosition will never equal ExactPosition on XNA
            
            // Answer by noel
            // I just fixed that - thanks for the details. it no longer uses equal check on floating points, but checks for distance being < 0.01f
            if (Hold.IsHeld || !(Speed == Vector2.Zero) || !(LiftSpeed == Vector2.Zero) ||
                Vector2.Distance(previousPosition, ExactPosition) >= 0.01f || !OnGround()) {
                return;
            }

            int num = Math.Sign(X - spinner.X);
            if (num == 0) {
                num = 1;
            }

            Speed.X = num * 120f;
            Speed.Y = -30f;
        }
    }
}