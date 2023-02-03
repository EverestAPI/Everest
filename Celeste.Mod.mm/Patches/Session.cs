#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;

namespace Celeste {
    public class patch_Session : Session {
        public bool RestartedFromGolden;

        public new patch_MapData MapData;

        public patch_Session(AreaKey area, string checkpoint = null, AreaStats oldStats = null)
            : base(area, checkpoint, oldStats) {
        }

        public extern void orig_ctor(AreaKey area, string checkpoint = null, AreaStats oldStats = null);

        [MonoModConstructor]
        public void ctor(AreaKey area, string checkpoint = null, AreaStats oldStats = null) {
            patch_AreaData areaData = patch_AreaData.Get(area);
            if (area.Mode == AreaMode.Normal) {
                areaData.RestoreASideAreaData();
            } else {
                areaData.OverrideASideMeta(area.Mode);
            }
            orig_ctor(area, checkpoint, oldStats);
        }
    }
}