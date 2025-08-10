#pragma warning disable CS0108 // Member is never used

using Microsoft.Xna.Framework;
using MonoMod;

namespace Monocle {
    public class patch_Grid : Grid {
        public patch_Grid(int cellsX, int cellsY, float cellWidth, float cellHeight) : base(cellsX, cellsY, cellWidth, cellHeight)
        {
            // no-op, ignored by MonoMod
        }
        
        [MonoModReplace] // Avoid virtual calls and redundant bounds checks - Everest adds bounds checks to VirtualMap already.
        public override bool Collide(Vector2 point) {
            var pos = point - AbsolutePosition;
            return Data[(int)(pos.X / CellWidth), (int)(pos.Y / CellHeight)];
        }
    } 
}
