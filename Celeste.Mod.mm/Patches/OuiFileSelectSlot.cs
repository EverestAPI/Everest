#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_OuiFileSelectSlot : OuiFileSelectSlot {

        // We're effectively in OuiFileSelectSlot, but still need to "expose" private fields to our mod.
        private OuiFileSelect fileSelect;
        private List<patch_Button> buttons;

        private patch_Button NewGameLevelSetButton;
        private string NewGameLevelSet;

        public patch_OuiFileSelectSlot(int index, OuiFileSelect fileSelect, SaveData data)
            : base(index, fileSelect, data) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // Patching constructors is ugly.
        public extern void orig_ctor(int index, OuiFileSelect fileSelect, SaveData data);
        [MonoModConstructor]
        public void ctor(int index, OuiFileSelect fileSelect, SaveData data) {
            // Temporarily set the current save data to the file slot's save data.
            // This enables filtering the areas by the save data's current levelset.
            SaveData prev = SaveData.Instance;
            SaveData.Instance = data;
            orig_ctor(index, fileSelect, data);
            SaveData.Instance = prev;
        }

        public extern void orig_CreateButtons();
        public new void CreateButtons() {
            orig_CreateButtons();

            if (Everest.Flags.Disabled || !CoreModule.Settings.ShowModOptionsInGame)
                return;

            if (!Exists) {

                if (AreaData.Areas.Select(area => area.GetLevelSet()).Distinct().Count() > 1) {
                    buttons.Add(NewGameLevelSetButton = new patch_Button() {
                        Label = DialogExt.CleanLevelSet(NewGameLevelSet ?? "Celeste"),
                        Scale = 0.5f,
                        Action = () => {
                            if (NewGameLevelSet == null)
                                NewGameLevelSet = "Celeste";

                            int id = AreaData.Areas.FindLastIndex(area => area.GetLevelSet() == NewGameLevelSet) + 1;
                            if (id >= AreaData.Areas.Count)
                                id = 0;
                            NewGameLevelSet = AreaData.Areas[id].GetLevelSet();

                            NewGameLevelSetButton.Label = DialogExt.CleanLevelSet(NewGameLevelSet ?? "Celeste");
                        }
                    });
                }

            }
        }

        public extern void orig_OnNewGameSelected();
        public void OnNewGameSelected() {
            orig_OnNewGameSelected();

            if (NewGameLevelSet != null && NewGameLevelSet != "Celeste") {
                SaveData.Instance.LastArea =
                    AreaData.Areas.FirstOrDefault(area => area.GetLevelSet() == NewGameLevelSet)?.ToKey() ??
                    AreaKey.Default;
            }
        }

        [MonoModReplace]
        private IEnumerator EnterFirstAreaRoutine() {
            // Replace ID 0 with SaveData.Instance.LastArea.ID

            Overworld overworld = fileSelect.Overworld;
            AreaData area = AreaData.Areas[SaveData.Instance.LastArea.ID];
            if (NewGameLevelSet != null && NewGameLevelSet != "Celeste") {
                // Pretend that we've beaten Prologue.
                LevelSetStats stats = SaveData.Instance.GetLevelSetStatsFor("Celeste");
                stats.UnlockedAreas = 1;
            }

            yield return fileSelect.Leave(null);

            yield return overworld.Mountain.EaseCamera(0, area.MountainIdle);
            yield return 0.3f;

            overworld.Mountain.EaseCamera(0, area.MountainZoom, 1f);
            yield return 0.4f;

            area.Wipe(overworld, false, null);
            overworld.RendererList.UpdateLists();
            overworld.RendererList.MoveToFront(overworld.Snow);

            yield return 0.5f;

            LevelEnter.Go(new Session(SaveData.Instance.LastArea), false);
        }

        // Required because Button is private.
        [MonoModIgnore]
        private class patch_Button {
            public string Label;
            public Action Action;
            public float Scale = 1f;
        }

    }
}
