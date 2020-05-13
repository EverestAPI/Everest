using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    public class OuiHelper_ChapterSelect_Reload : Oui {
        internal static string AreaReloadLock = "lock";

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
            Reload(true);
        }

        public static void Reload(bool recrawl) {
            SaveData saveData = SaveData.Instance;

            // ChapterSelect only updates the ID.
            string lastAreaSID = saveData == null ? null : (AreaData.Get(saveData.LastArea.ID)?.ToKey().GetSID() ?? AreaKey.Default.GetSID());
            // Note: SaveData.Instance.LastArea is reset by AreaData.Interlude_Safe -> SaveData.LevelSetStats realizing that AreaOffset == -1
            // Store the "resolved" last selected area in a local variable, then re-set it after reloading.

            // Reload all maps.
            if (recrawl)
                Everest.Content.Recrawl();

            lock (AreaReloadLock) { // prevent anything from calling AreaData.Get during this.
                AreaData.Unload();
                AreaData.Load();
                AreaData.ReloadMountainViews();

                // Fake a save data reload to resync the save data to the new area list.
                if (saveData != null) {
                    AreaData lastArea = AreaDataExt.Get(lastAreaSID);
                    saveData.LastArea = lastArea?.ToKey() ?? AreaKey.Default;
                    saveData.BeforeSave();
                    saveData.AfterInitialize();
                }
            }

            if (Engine.Scene is Overworld overworld) {
                if (overworld.Mountain.Area >= AreaData.Areas.Count)
                    overworld.Mountain.EaseCamera(0, AreaData.Areas[0].MountainIdle, null, true);

                OuiChapterSelect chapterSelect = overworld.GetUI<OuiChapterSelect>();
                overworld.UIs.Remove(chapterSelect);
                overworld.Remove(chapterSelect);

                chapterSelect = new OuiChapterSelect();
                chapterSelect.Visible = false;
                overworld.Add(chapterSelect);
                overworld.UIs.Add(chapterSelect);
                chapterSelect.IsStart(overworld, (Overworld.StartMode) (-1));
            }
        }

    }
}
