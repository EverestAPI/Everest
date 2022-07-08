using Microsoft.Xna.Framework;
using MonoMod;
using System.Collections;

namespace Celeste {
    class patch_MoveBlock : MoveBlock {

        public patch_MoveBlock(Vector2 position, int width, int height, MoveBlock.Directions direction, bool canSteer, bool fast)
            : base(position, width, height, direction, canSteer, fast) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchMoveBlockController]
        private extern IEnumerator Controller();
    }
}
