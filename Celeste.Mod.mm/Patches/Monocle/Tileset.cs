#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using MonoMod;

namespace Monocle {
    /// <summary>
    /// Add bounds checking (wrap indices using %) to indexers.
    /// </summary>
    public class patch_Tileset : Tileset {
        private MTexture[,] tiles;

        [MonoModIgnore]
        public patch_Tileset(MTexture texture, int tileWidth, int tileHeight)
            : base(texture, tileWidth, tileHeight) {}

        public new MTexture this[int x, int y] {
            [MonoModReplace]
            get {
                return tiles[x % tiles.GetLength(0), y % tiles.GetLength(1)];
            }
        }

        public new MTexture this[int index] {
            [MonoModReplace]
            get {
                if (index < 0)
                    return null;
                return tiles[index % tiles.GetLength(0), (index / tiles.GetLength(0)) % tiles.GetLength(1)];
            }
        }
    }
}
