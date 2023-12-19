using MonoMod;

namespace Microsoft.Xna.Framework {
    [GameDependencyPatch("FNA")]
    struct patch_Color {

        // The following signatures existed in older versions of FNA when they shouldn't have.


        [MonoModConstructor] // we want to call the original constructor
        [MonoModIgnore] // this is necessary to prevent Monomod from generating redundant orig_ctor
        public extern void ctor(int r, int g, int b, int alpha);

        [MonoModConstructor]
        public void ctor(Color color, int alpha) {
            ctor(color.R, color.G, color.B, (byte) int.Clamp(alpha, byte.MinValue, byte.MaxValue));
        }

        [MonoModConstructor]
        public void ctor(Color color, float alpha) {
            ctor(color.R, color.G, color.B, (byte) float.Clamp(alpha * 255, byte.MinValue, byte.MaxValue));
        }
    }
}
