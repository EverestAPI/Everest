using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_GoldenBlock : GoldenBlock {

        private float renderLerp;

        public patch_GoldenBlock(EntityData data, Vector2 offset) : base(data, offset) {
            // no-op.
        }

        public extern void orig_Update();
        public override void Update() {
            orig_Update();
            if (renderLerp == 0)
                EnableStaticMovers();
            else
                DisableStaticMovers();
        }

        [MonoModIgnore] // we don't want to change anything in the method...
        [PatchGoldenBlockStaticMovers] // ... except manipulating it manually with MonoModRules
        public extern override void Awake(Scene scene);
    }
}
