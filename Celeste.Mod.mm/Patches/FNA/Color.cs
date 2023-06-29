using MonoMod;

namespace Microsoft.Xna.Framework {
    [GameDependencyPatch("FNA")]
    struct patch_Color {

        // The following signatures existed in older versions of FNA when they shouldn't have.

        [MonoModLinkFrom("System.Void Microsoft.Xna.Framework.Color::.ctor(Microsoft.Xna.Framework.Color,System.Int32)")]
        public static Color NewColor(Color color, int alpha) => new Color(color.R, color.G, color.B, alpha);

        [MonoModLinkFrom("System.Void Microsoft.Xna.Framework.Color::.ctor(Microsoft.Xna.Framework.Color,System.Single)")]
        public static Color NewColor(Color color, float alpha) => new Color(color.R, color.G, color.B, (byte) alpha * 255);

    }
}
