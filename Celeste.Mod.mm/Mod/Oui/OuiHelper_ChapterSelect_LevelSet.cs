using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
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
            string startLevelSet = SaveData.Instance.GetLevelSet();
            int count = AreaData.Areas.Count;
            for (int i = (count + startID + Direction) % count; i != startID; i = (count + i + Direction) % count) {
                AreaData area = AreaData.Areas[i];
                if (area.GetLevelSet() != startLevelSet) {
                    SaveData.Instance.LastArea = area.ToKey();
                    goto Done;
                }
            }

            Done:
            Overworld.Goto<OuiChapterSelect>();
            yield break;
        }

        public override IEnumerator Leave(Oui next) {
            yield break;
        }

    }
}
