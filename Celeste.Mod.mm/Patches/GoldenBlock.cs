#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_GoldenBlock : GoldenBlock {

#pragma warning disable CS0649 // field is never assigned and will always be null: it is initialized in vanilla code
        private float renderLerp;
#pragma warning restore CS0649

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
