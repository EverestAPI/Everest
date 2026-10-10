#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste;
using Microsoft.Xna.Framework;
using MonoMod;

namespace Monocle {
    class patch_TileGrid : TileGrid {

        public VirtualMap<char> TileIds;

        public patch_TileGrid() : base(0, 0, 0, 0) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(int tileWidth, int tileHeight, int tilesX, int tilesY);
        [MonoModConstructor]
        public void ctor(int tileWidth, int tileHeight, int tilesX, int tilesY) {
            orig_ctor(tileWidth, tileHeight, tilesX, tilesY);
            TileIds = new VirtualMap<char>(tilesX, tilesY);
        }

        // improve tile grid rendering performance
        public new void RenderAt(Vector2 position) {
            if (Alpha <= 0f) {
                return;
            }

            // Many entities (both vanilla and modded) don't set this field, which gets rid of culling.
            // Let's just set this to the most obvious value...
            if (ClipCamera is null && Scene is Level lvl) {
                ClipCamera = lvl.Camera;
            }

            Rectangle clippedRenderTiles = GetClippedRenderTiles();
            int tileWidth = TileWidth;
            int tileHeight = TileHeight;
            Color color = Color * Alpha;
            Vector2 renderPos = new Vector2(position.X + clippedRenderTiles.Left * tileWidth, position.Y + clippedRenderTiles.Top * tileHeight);

            for (int i = clippedRenderTiles.Left; i < clippedRenderTiles.Right; i++) {
                for (int j = clippedRenderTiles.Top; j < clippedRenderTiles.Bottom; j++) {
                    MTexture mtexture = Tiles[i, j];
                    if (mtexture != null) {
                        Draw.SpriteBatch.Draw(mtexture.Texture.Texture, renderPos, mtexture.ClipRect, color);
                    }
                    renderPos.Y += tileHeight;
                }
                renderPos.X += tileWidth;
                renderPos.Y = position.Y + clippedRenderTiles.Top * tileHeight;
            }
        }

        public char TilesetIdAt(Vector2 readPosition) {
            Vector2 position = Entity.Position + Position;
            int num = (int) ((readPosition.X - position.X) / 8f);
            int num2 = (int) ((readPosition.Y - position.Y) / 8f);
            if (num >= 0 && num2 >= 0 && num < TilesX && num2 < TilesY) {
                return TileIds[num, num2];
            }
            return default;
        }
    }
}
