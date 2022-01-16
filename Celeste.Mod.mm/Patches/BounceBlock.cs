#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_BounceBlock : BounceBlock {

        // We're effectively in BounceBlock, but still need to "expose" private fields to our mod.
        private bool iceMode;
        private bool iceModeNext;

        private bool notCoreMode;

        [MonoModIgnore]
        public extern patch_BounceBlock(Vector2 position, float width, float height);

        [MonoModConstructor]
        [MonoModReplace]
        public patch_BounceBlock(EntityData data, Vector2 offset)
            : this(data.Position + offset, data.Width, data.Height) {
            notCoreMode = data.Bool("notCoreMode");
        }

        [MonoModLinkTo("Monocle.Entity", "Added")]
        [MonoModIgnore]
        public extern void base_Added(Scene scene);
        [MonoModReplace]
        public override void Added(Scene scene) {
            base_Added(scene);
            iceModeNext = iceMode = SceneAs<Level>().CoreMode == Session.CoreModes.Cold || notCoreMode;
            ToggleSprite();
        }

        [MonoModIgnore]
        private extern void ToggleSprite();

    }
}
