#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;

namespace Celeste {
    // ClutterBlockGenerator is static, so we cannot extend it.
    public static class patch_ClutterBlockGenerator {
        // expose this private struct and field to our mod.
        private struct Tile { }

#pragma warning disable CS0649 // field is never initialized
        private static Tile[,] tiles;
        private static bool initialized;
#pragma warning restore CS0649 // field is never initialized

        public static extern void orig_Init(Level lvl);
        public static void Init(Level lvl) {
            // like vanilla, skip everything if the generator was already initialized.
            if (initialized) {
                return;
            }

            // like vanilla, initialize the tile array at 200x200 size if not initialized yet.
            if (tiles == null) {
                tiles = new Tile[200, 200];
            }

            // check that the tile array is big enough
            int neededWidth = lvl.Bounds.Width / 8;
            int neededHeight = lvl.Bounds.Height / 8 + 1;
            if (tiles.GetLength(0) < neededWidth || tiles.GetLength(1) < neededHeight) {
                // if not, extend it as required.
                int newWidth = Math.Max(tiles.GetLength(0), neededWidth);
                int newHeight = Math.Max(tiles.GetLength(1), neededHeight);

                tiles = new Tile[newWidth, newHeight];
            }

            // carry on with vanilla.
            orig_Init(lvl);
        }
    }
}
