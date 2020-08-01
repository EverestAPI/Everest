using Microsoft.Xna.Framework;
using MonoMod;

namespace Celeste {
    class patch_Water : Water {

        public patch_Water(EntityData data, Vector2 offset) : base(data, offset) {
        }

        [MonoModIgnore] // we don't want to change anything in the method...
        [PatchWaterUpdate] // ... except manipulating it manually with MonoModRules
        public extern override void Update();

    }
}
