using Celeste.Mod.Helpers;

namespace Celeste {
    class patch_LavaRect : LavaRect {

        public patch_LavaRect(float width, float height, int step) : base(width, height, step) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        private bool IsVisible() {
            var renderPos = Entity.Position + Position;
            return CullHelper.IsRectangleVisible(renderPos.X, renderPos.Y, Width, Height, lenience: 8f);
        }
    }
}