namespace Celeste {
    class patch_LevelSetStats : LevelSetStats {
        public int MaxAreaMode { 
            get {
                if (Name == "Celeste") {
                    return (int) AreaMode.CSide;
                }
                int areaOffset = AreaOffset;
                int maxAreaMode = 0;
                for (int i = 0; i <= MaxArea; i++) {
                    ModeProperties[] mode = AreaData.Areas[areaOffset + i].Mode;
                    foreach (ModeProperties modeProperties in mode) {
                        if ((int) modeProperties.MapData.Area.Mode > maxAreaMode) {
                            maxAreaMode = (int) modeProperties.MapData.Area.Mode;
                        }
                    }
                }
                return maxAreaMode;
            }
        }
    }
}