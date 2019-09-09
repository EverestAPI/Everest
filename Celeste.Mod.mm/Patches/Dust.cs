using Microsoft.Xna.Framework;

namespace Celeste {
    // Dust is static.
    class patch_Dust {
        // Make this signature accessible to older mods.
        public static void Burst(Vector2 position, float direction, int count = 1) {
            Dust.Burst(position, direction, count, null);
        }
    }
}
