using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    public class patch_StrawberryPoints : StrawberryPoints {
        public patch_StrawberryPoints(Vector2 position, bool ghostberry, int index, bool moonberry)
            : base(position, ghostberry, index, moonberry) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchStrawberryPointsAdded]
        public override extern void Added(Scene scene);
    }
}
