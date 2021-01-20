#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste {
    class patch_Cobweb : Cobweb {

        public Color[] OverrideColors;

        public patch_Cobweb(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            AreaData area = AreaData.Get(scene);

            Color[] prevColors = area.CobwebColor;
            if (OverrideColors != null)
                area.CobwebColor = OverrideColors;

            orig_Added(scene);

            area.CobwebColor = prevColors;
        }

    }
}
