using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    // Extend Entity because of base.Added() and because of Constructor
    class patch_BackgroundTiles : Entity {
        public TileGrid Tiles;
        public AnimatedTiles AnimatedTiles;

        [MonoModReplace]
        [MonoModConstructor]
        public patch_BackgroundTiles(Vector2 position, VirtualMap<char> data) {
            Position = position;
            Tag = Tags.Global;
            Autotiler.Generated generated = GFX.BGAutotiler.GenerateMap(data, paddingIgnoreOutOfLevel: false);
            Tiles = generated.TileGrid;
            Tiles.VisualExtend = 1;
            Add(Tiles);
            Add(AnimatedTiles = generated.SpriteOverlay);
            Depth = Depths.BGTerrain;
        }

        [MonoModReplace]
        public override void Added(Scene scene) {
            base.Added(scene);
            Tiles.ClipCamera = SceneAs<Level>().Camera;
            AnimatedTiles.ClipCamera = Tiles.ClipCamera;
        }

    }
}
