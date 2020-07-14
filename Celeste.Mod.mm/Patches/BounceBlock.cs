#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    // : Solid because base.Added
    class patch_BounceBlock : Solid {

        // We're effectively in IntroCrusher, but still need to "expose" private fields to our mod.
        private bool iceMode;
        private bool iceModeNext;

        private bool notCoreMode;

        public patch_BounceBlock(EntityData data, Vector2 offset) 
            : base(data.Position + offset, data.Width, data.Height, false) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(EntityData data, Vector2 offset);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset) {
            orig_ctor(data, offset);

            notCoreMode = data.Bool("notCoreMode");
        }

        [MonoModReplace]
        public override void Added(Scene scene) {
            base.Added(scene);
            iceModeNext = iceMode = SceneAs<Level>().CoreMode == Session.CoreModes.Cold || notCoreMode;
            ToggleSprite();
        }

        [MonoModIgnore]
        private extern void ToggleSprite();

    }
}
