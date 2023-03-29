using Microsoft.Xna.Framework;
using MonoMod;
using System;

namespace Monocle {
    // Patch XNA/FNA float jank differences
    struct patch_SimpleCurve {

        public Vector2 Begin, End, Control;

        [MonoModReplace]
        public void DoubleControl() {
            Control = new Vector2(
                (float) ((double) Control.X + Control.X - (Begin.X + ((double) End.X - Begin.X) / 2.0)),
                (float) ((double) Control.Y + Control.Y - (Begin.Y + ((double) End.Y - Begin.Y) / 2.0))
            );
        }

        // Separate high-precision calculation method as to not have to inline this into LengthParametric
        private void __GetPoint(double percent, out double outX, out double outY) {
            double num = 1.0 - percent;
            outX = num * num * Begin.X + 2.0 * num * percent * Control.X + percent * percent * End.X;
            outY = num * num * Begin.Y + 2.0 * num * percent * Control.Y + percent * percent * End.Y;
        }

        [MonoModReplace]
        public Vector2 GetPoint(float percent) {
            __GetPoint(percent, out double x, out double y);
            return new Vector2((float) x, (float) y);
        }

        [MonoModReplace]
        public float GetLengthParametric(int resolution) {
            Vector2 vector = Begin;
            float num = 0f;
            for (int i = 1; i <= resolution; i++) {
                __GetPoint((double) i / resolution, out double pointX, out double pointY);
                num += (float) Math.Sqrt((pointX - vector.X)*(pointX - vector.X) + (pointY - vector.Y)*(pointY - vector.Y));
                vector = new Vector2((float) pointX, (float) pointY);
            }
            return num;
        }

    }
}