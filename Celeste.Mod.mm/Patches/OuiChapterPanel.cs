#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_OuiChapterPanel : OuiChapterPanel {

        private bool instantClose = false;

        [MonoModReplace]
        public static new string GetCheckpointPreviewName(AreaKey area, string level) {
            string result = area.ToString();
            if (area.ID >= 10)
                result = area.GetSID();
            if (level != null)
                result = result + "_" + level;

            if (GFX.Checkpoints.Has(result))
                return result;
            return $"{area.GetSID()}/{(char) ('A' + (int) area.Mode)}/{level ?? "start"}";
        }

        public extern bool orig_IsStart(Overworld overworld, Overworld.StartMode start);
        public override bool IsStart(Overworld overworld, Overworld.StartMode start) {
            if (SaveData.Instance != null && SaveData.Instance.LastArea.ID == AreaKey.None.ID) {
                SaveData.Instance.LastArea = AreaKey.Default;
                instantClose = true;
            }
            return orig_IsStart(overworld, start);
        }

        public extern IEnumerator orig_Enter(Oui from);
        public override IEnumerator Enter(Oui from) {
            if (instantClose)
                return EnterClose(from);
            return orig_Enter(from);
        }

        private IEnumerator EnterClose(Oui from) {
            Overworld.Goto<OuiChapterSelect>();
            Visible = false;
            instantClose = false;
            yield break;
        }

        public extern void orig_Update();
        public override void Update() {
            if (instantClose) {
                Overworld.Goto<OuiChapterSelect>();
                Visible = false;
                instantClose = false;
                return;
            }
            orig_Update();
        }

        [MonoModReplace]
        private IEnumerator StartRoutine(string checkpoint = null) {
            Overworld.Maddy.Hide(false);
            Overworld.Mountain.EaseCamera(Area.ID, Data.MountainZoom, 1f);
            Add(new Coroutine(EaseOut(false)));
            yield return 0.2f;

            AreaData.Get(Area).Wipe(Overworld, false, null);
            Audio.SetMusic(null);
            Audio.SetAmbience(null);
            // TODO: Determine if the area should keep the overworld snow.
            if ((Area.ID == 0 || Area.ID == 9) && checkpoint == null && Area.Mode == AreaMode.Normal) {
                Overworld.RendererList.UpdateLists();
                Overworld.RendererList.MoveToFront(Overworld.Snow);
            }
            yield return 0.5f;

            try {
                LevelEnter.Go(new Session(Area, checkpoint), false);
            } catch (Exception e) {
                Mod.Logger.Log(LogLevel.Warn, "misc", $"Failed entering area {Area}");
                e.LogDetailed();

                string message = Dialog.Get("postcard_levelloadfailed")
                    .Replace("((player))", SaveData.Instance.Name)
                    .Replace("((sid))", Area.GetSID())
                ;

                LevelEnterExt.ErrorMessage = message;
                LevelEnter.Go(new Session(new AreaKey(1).SetSID("")), false);
            }
        }

    }
}
