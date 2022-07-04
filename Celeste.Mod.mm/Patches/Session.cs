#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;

namespace Celeste {
    public class patch_Session {
        public extern void orig_ctor(AreaKey area, string checkpoint = null, AreaStats oldStats = null);

        [MonoModIgnore]
        public string Level;

        private LevelData levelData;

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

        public extern LevelData orig_get_LevelData();
        public LevelData get_LevelData() {
            if (levelData is null || levelData.Name != Level) {
                levelData = orig_get_LevelData();
            }
            return levelData;
        }
    }
}