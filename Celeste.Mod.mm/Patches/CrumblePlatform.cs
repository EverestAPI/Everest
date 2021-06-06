#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste {
    class patch_CrumblePlatform : CrumblePlatform {

        public string OverrideTexture;

        public patch_CrumblePlatform(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            AreaData area = AreaData.Get(scene);

            string prevTexture = area.CrumbleBlock;
            if (OverrideTexture != null)
                area.CrumbleBlock = OverrideTexture;

            orig_Added(scene);

            area.CrumbleBlock = prevTexture;
        }

    }
}
