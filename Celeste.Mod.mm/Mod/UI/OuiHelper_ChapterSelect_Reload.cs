using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    public class OuiHelper_ChapterSelect_Reload : Oui {

        public OuiHelper_ChapterSelect_Reload() {
        }

        public override IEnumerator Enter(Oui from) {
            yield return 0.25f;

            Reload();

            Audio.Play(SFX.ui_world_whoosh_400ms_back);
            Overworld.Goto<OuiChapterSelect>();
        }

        public override IEnumerator Leave(Oui next) {
            yield break;
        }

        public static void Reload() {
            // ChapterSelect only updates the ID.
            string lastAreaSID = AreaData.Get(SaveData.Instance.LastArea.ID)?.ToKey().GetSID() ?? AreaKey.Default.GetSID();
            // Note: SaveData.Instance.LastArea is reset by AreaData.Interlude_Safe -> SaveData.LevelSetStats realizing that AreaOffset == -1
            // Store the "resolved" last selected area in a local variable, then re-set it after reloading.

            // Reload all maps.
            Everest.Content.Recrawl();
            AreaData.Unload();
            AreaData.Load();
            AreaData.ReloadMountainViews();

            // Fake a save data reload to resync the save data to the new area list.
            AreaData lastArea = AreaDataExt.Get(lastAreaSID);
            SaveData.Instance.LastArea = lastArea?.ToKey() ?? AreaKey.Default;
            SaveData.Instance.BeforeSave();
            SaveData.Instance.AfterInitialize();

            Overworld overworld = (Engine.Scene.Entities.FindFirst<Oui>())?.Overworld;
            if (overworld == null)
                return;
            if (overworld.Mountain.Area >= AreaData.Areas.Count)
                overworld.Mountain.EaseCamera(0, AreaData.Areas[0].MountainIdle, null, true);
            overworld.ReloadMenus((Overworld.StartMode) (-1));
        }

    }
}
