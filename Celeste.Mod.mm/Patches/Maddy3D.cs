#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System.Collections.Generic;
using Monocle;

namespace Celeste {
    public class patch_Maddy3D : Maddy3D {
        private List<MTexture> frames;

        public patch_Maddy3D(MountainRenderer renderer) : base(renderer) { }

        private extern void orig_SetRunAnim();

        private void SetRunAnim() {
            if (Renderer.Area < 0 || AreaData.Get(Renderer.Area).IsOfficialLevelSet()) {
                orig_SetRunAnim();
            }
            else if (AreaData.Get(Renderer.Area).Mode[0].Inventory.Dashes > 1) {
                frames = GFX.Mountain.GetAtlasSubtextures("marker/runNoBackpack");
            }
            else {
                frames = GFX.Mountain.GetAtlasSubtextures("marker/runBackpack");
            }
        }
    }
}