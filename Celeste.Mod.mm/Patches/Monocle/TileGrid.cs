using Microsoft.Xna.Framework;

namespace Monocle {
    class patch_TileGrid : TileGrid {

        public patch_TileGrid() : base(0, 0, 0, 0) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // improve tile grid rendering performance
        public new void RenderAt(Vector2 position) {
            if (Alpha <= 0f) {
                return;
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
    }
}
