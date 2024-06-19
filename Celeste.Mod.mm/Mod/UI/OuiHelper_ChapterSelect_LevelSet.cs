using System;
using System.Collections;

namespace Celeste.Mod.UI {
    public class OuiHelper_ChapterSelect_LevelSet : Oui {

        public int Direction;

        public OuiHelper_ChapterSelect_LevelSet() {
        }

        public override IEnumerator Enter(Oui from) {
            if (Direction == 0) {
                Overworld.Goto<OuiChapterSelect>();
                goto Done;
            }
            Direction = Math.Sign(Direction);

            yield return 0.25f;

            int startID = SaveData.Instance.LastArea.ID;
            string startLevelSet = patch_SaveData.Instance.LevelSet;
            int count = AreaData.Areas.Count;
            for (int i = (count + startID + Direction) % count; i != startID; i = (count + i + Direction) % count) {
                patch_AreaData area = patch_AreaData.Get(i);
                if (area == null || area.LevelSet != startLevelSet) {
                    SaveData.Instance.LastArea = area.ToKey();
                    goto Done;
                }
            }

            Done:
            if (Direction > 0) {
                Audio.Play(SFX.ui_world_chapter_pane_expand);
            } else {
                Audio.Play(SFX.ui_world_chapter_pane_contract);
            }
            Overworld.Goto<OuiChapterSelect>();
        }

        public override IEnumerator Leave(Oui next) {
            yield break;
        }

    }
}
