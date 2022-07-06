#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;

namespace Celeste {
    public class patch_Session : Session {
        public extern void orig_ctor(AreaKey area, string checkpoint = null, AreaStats oldStats = null);

        private LevelData levelData;
        private uint leveldata_cache_validity = 0;

        public patch_Session(AreaKey area, string checkpoint = null, AreaStats oldStats = null)
            : base(area, checkpoint, oldStats) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModConstructor]
        public void ctor(AreaKey area, string checkpoint = null, AreaStats oldStats = null) {
            AreaData areaData = AreaData.Get(area);
            if (area.Mode == AreaMode.Normal) {
                areaData.RestoreASideAreaData();
            } else {
                areaData.OverrideASideMeta(area.Mode);
            }
            orig_ctor(area, checkpoint, oldStats);
        }

        public new LevelData LevelData {
            [MonoModReplace]
            get {
                if (levelData is null || levelData.Name != Level || ((patch_MapData) MapData).session_leveldata_cache_validity != leveldata_cache_validity) {
                    levelData = MapData.Get(Level);
                    leveldata_cache_validity = ((patch_MapData) MapData).session_leveldata_cache_validity;
                }
                return levelData;
            }
        }
    }
}