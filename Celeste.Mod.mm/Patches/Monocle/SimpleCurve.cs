using Microsoft.Xna.Framework;
using MonoMod;

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

        [MonoModReplace]
        public Vector2 GetPoint(float percent) {
            double num = 1.0 - percent;
            return new Vector2(
                (float) ((double) (float) (num * num) * Begin.X + (float) (2.0 * num * percent) * Control.X + (float) (percent * percent) * End.X),
                (float) ((double) (float) (num * num) * Begin.Y + (float) (2.0 * num * percent) * Control.Y + (float) (percent * percent) * End.Y)
            );
        }

        [MonoModReplace]
        public float GetLengthParametric(int resolution) {
            Vector2 vector = Begin;
            float num = 0f;
            for (int i = 1; i <= resolution; i++) {
                Vector2 point = GetPoint((float) i / resolution);
                num += (point - vector).Length();
                vector = point;
            }
            return num;
        }

    }
}