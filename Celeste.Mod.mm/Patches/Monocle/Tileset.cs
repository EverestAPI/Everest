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
        
        // patch compiler generated methods directly to circumvent MonoMod issue with patching properties with identical names
        [MonoModReplace]
        public MTexture get_Item(int x, int y) => tiles[x % tiles.GetLength(0), y % tiles.GetLength(1)];

        [MonoModReplace]
        public MTexture get_Item(int index) {
            if (index < 0)
                return null;
            return tiles[index % tiles.GetLength(0), (index / tiles.GetLength(0)) % tiles.GetLength(1)];
        }
    }
}
