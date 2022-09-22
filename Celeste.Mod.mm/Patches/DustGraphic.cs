using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_DustGraphic : DustGraphic {

        public patch_DustGraphic(bool ignoreSolids, bool autoControlEyes, bool autoExpandDust) : base(ignoreSolids, autoControlEyes, autoExpandDust) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        private bool InView {
            [MonoModReplace]
            get {
                if (Scene == null || Entity == null) // null check to fix crashing under strange circumstance (parent entity removed during transition)
                    return false;
                Camera camera = (Scene as Level).Camera;
                Vector2 position = Entity.Position;
                return !(position.X + 16f < camera.Left)
                       && !(position.Y + 16f < camera.Top)
                       && !(position.X - 16f > camera.Right)
                       && !(position.Y - 16f > camera.Bottom);
            }
        }
    }
}
