#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Monocle;
using MonoMod;
using System.Collections.Generic;

namespace Celeste {
    class patch_ReturnMapHint : ReturnMapHint {

        private MTexture checkpoint;

        [MonoModReplace]
        public static new string GetCheckpointPreviewName(AreaKey area, string level) {
            return patch_OuiChapterPanel._GetCheckpointPreviewName(area, level);
        }

        public extern void orig_Added(Scene scene);
        public new void Added(Scene scene) {
            orig_Added(scene);

            Session session = (scene as Level)?.Session;
            if (checkpoint != null || session == null || session.Area.GetLevelSet() == "Celeste")
                return;

            AreaKey area = session.Area;
            ModeProperties mode = AreaData.Areas[area.ID].Mode[(int) area.Mode];
            if (mode.Checkpoints == null)
                return;

            HashSet<string> cps = SaveData.Instance.GetCheckpoints(area);
            CheckpointData cp = null;

            foreach (CheckpointData checkpointData2 in mode.Checkpoints) {
                bool flag2 = session.LevelFlags.Contains(checkpointData2.Level) && cps.Contains(checkpointData2.Level);
                if (flag2) {
                    cp = checkpointData2;
                }
            }

            string id = GetCheckpointPreviewName(area, cp?.Level);
            if (MTN.Checkpoints.Has(id)) {
                checkpoint = MTN.Checkpoints[id];
            }
        }

    }
}
