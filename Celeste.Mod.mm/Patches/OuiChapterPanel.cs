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
            return _GetCheckpointPreviewName(area, level);
        }

        internal static string _GetCheckpointPreviewName(AreaKey area, string level) {
            int split = level?.IndexOf('|') ?? -1;
            if (split >= 0) {
                area = AreaDataExt.Get(level.Substring(0, split))?.ToKey(area.Mode) ?? area;
                level = level.Substring(split + 1);
            }

            string result = area.ToString();
            if (area.GetLevelSet() != "Celeste")
                result = area.GetSID();

            if (level != null)
                result = result + "_" + level;

            if (MTN.Checkpoints.Has(result))
                return result;

            return $"{area.GetSID()}/{(char) ('A' + (int) area.Mode)}/{level ?? "start"}";
        }

        public extern bool orig_IsStart(Overworld overworld, Overworld.StartMode start);
        public override bool IsStart(Overworld overworld, Overworld.StartMode start) {
            if (SaveData.Instance != null && SaveData.Instance.LastArea.ID == AreaKey.None.ID) {
                SaveData.Instance.LastArea = AreaKey.Default;
                instantClose = true;
            }

            if (start == Overworld.StartMode.AreaComplete || start == Overworld.StartMode.AreaQuit) {
                AreaData area = AreaData.Get(SaveData.Instance.LastArea.ID);
                area = AreaDataExt.Get(area?.GetMeta()?.Parent) ?? area;
                if (area != null)
                    SaveData.Instance.LastArea.ID = area.ID;
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

        [MonoModIgnore]
        [PatchChapterPanelSwapRoutine]
        private extern IEnumerator SwapRoutine();

        private static HashSet<string> _GetCheckpoints(SaveData save, AreaKey area) {
            // TODO: Maybe switch back to using SaveData.GetCheckpoints in the future?

            if (Celeste.PlayMode == Celeste.PlayModes.Event)
                return new HashSet<string>();

            HashSet<string> set;

            AreaData areaData = AreaData.Areas[area.ID];
            ModeProperties mode = areaData.Mode[(int) area.Mode];

            if (save.DebugMode || save.CheatMode) {
                set = new HashSet<string>();
                if (mode.Checkpoints != null)
                    foreach (CheckpointData cp in mode.Checkpoints)
                        set.Add($"{(AreaData.Get(cp.GetArea()) ?? areaData).GetSID()}|{cp.Level}");
                return set;

            }
            
            AreaModeStats areaModeStats = save.Areas[area.ID].Modes[(int) area.Mode];
            set = areaModeStats.Checkpoints;

            // Perform the same "cleanup" as SaveData.GetCheckpoints, but copy the set when adding area SIDs.
            if (mode == null) {
                set.Clear();
                return set;
            }
            
            set.RemoveWhere((string a) => !mode.Checkpoints.Any((CheckpointData b) => b.Level == a));
            AreaData[] subs = AreaData.Areas.Where(other =>
                other.GetMeta()?.Parent == areaData.GetSID() &&
                other.HasMode(area.Mode)
            ).ToArray();
            return new HashSet<string>(set.Select(s => {
                foreach (AreaData sub in subs) {
                    foreach (CheckpointData cp in sub.Mode[(int) area.Mode].Checkpoints) {
                        if (cp.Level == s) {
                            return $"{sub.GetSID()}|{s}";
                        }
                    }
                }
                return s;
            }));
        }

        [MonoModReplace]
        private IEnumerator StartRoutine(string checkpoint = null) {
            int checkpointAreaSplit = checkpoint?.IndexOf('|') ?? -1;
            if (checkpointAreaSplit >= 0) {
                Area = AreaDataExt.Get(checkpoint.Substring(0, checkpointAreaSplit))?.ToKey(Area.Mode) ?? Area;
                checkpoint = checkpoint.Substring(checkpointAreaSplit + 1);
            }

            EnteringChapter = true;
            Overworld.Maddy.Hide(false);
            Overworld.Mountain.EaseCamera(Area.ID, Data.MountainZoom, 1f);
            Add(new Coroutine(EaseOut(false)));
            yield return 0.2f;

            ScreenWipe.WipeColor = Color.Black;
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
                LevelEnter.Go(new Session(Area), false);
            }
        }

    }
}
