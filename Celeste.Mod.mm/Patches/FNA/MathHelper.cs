using MonoMod;
using System;

namespace Microsoft.Xna.Framework {
    [GameDependencyPatch("FNA")]
    struct patch_MathHelper {

        // Patch x87 floating point jank (see patch_VectorXYZ for more details)
        // Inlining might potentially affect SmoothStep - patch if that ever becomes an issue

        public static Color NewColor(Color color, int alpha) => new Color(color.R, color.G, color.B, alpha);
        public static Color NewColor(Color color, float alpha) => new Color(color.R, color.G, color.B, (byte) alpha * 255);

        [MonoModReplace]
        public static float Barycentric(float value1, float value2, float value3, float amount1, float amount2) {
            return (float) ((double) value1 + ((double) value2 - value1) * amount1 + ((double) value3 - value1) * amount2);
        }

        [MonoModReplace]
        public static float Lerp(float value1, float value2, float amount) {
            return (float) ((double) value1 + ((double) value2 - value1) * amount);
        }

        [MonoModReplace]
        public static float WrapAngle(float angle) {
            if (angle > -Math.PI && angle <= Math.PI)
                return angle;

            angle = (float) (angle % (Math.PI * 2.0));
            if (angle <= -Math.PI)
                return (float) (angle + Math.PI * 2.0);
            if (angle > Math.PI)
                return (float) (angle - Math.PI * 2.0);
            return angle;
        }

    }
}
