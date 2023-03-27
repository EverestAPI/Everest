using MonoMod;

namespace Microsoft.Xna.Framework {
    [GameDependencyPatch("FNA")]
    struct patch_Color {

        // The following signatures existed in older versions of FNA when they shouldn't have.

        public static Color NewColor(Color color, int alpha) => new Color(color.R, color.G, color.B, alpha);
        public static Color NewColor(Color color, float alpha) => new Color(color.R, color.G, color.B, (byte) alpha * 255);

    }
}
