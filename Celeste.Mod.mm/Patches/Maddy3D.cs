#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Monocle;
using System.Collections.Generic;
using System.IO;

namespace Celeste {
    public class patch_Maddy3D : Maddy3D {
        private List<MTexture> frames;

        public patch_Maddy3D(MountainRenderer renderer) : base(renderer) { }

        private extern void orig_SetRunAnim();

        private void SetRunAnim() {
            if (Renderer.Area < 0 || patch_AreaData.Get(Renderer.Area).IsOfficialLevelSet()) {
                orig_SetRunAnim();
            } else {
                string markerTexture = getMarkerTexture();
                if (markerTexture != null) {
                    frames = MTN.Mountain.GetAtlasSubtextures(markerTexture);
                } else if (AreaData.Get(Renderer.Area).Mode[0].Inventory.Dashes > 1) {
                    frames = MTN.Mountain.GetAtlasSubtextures("marker/runNoBackpack");
                } else {
                    frames = MTN.Mountain.GetAtlasSubtextures("marker/runBackpack");
                }
            }
        }

        private string getMarkerTexture() {
            string path = Path.Combine("Maps", patch_AreaData.Get(Renderer.Area).SID ?? "");
            return Everest.Content.TryGet(path, out ModAsset mapAsset) ? mapAsset.GetMeta<MapMeta>()?.Mountain?.MarkerTexture : null;
        }
    }
}