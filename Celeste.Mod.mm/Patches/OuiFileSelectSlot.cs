#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

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

        // computed maximums for stamp rendering
        private int maxStrawberryCount;
        private int maxGoldenStrawberryCount;
        private int maxStrawberryCountIncludingUntracked;
        private int maxCassettes;
        private int maxCrystalHeartsExcludingCSides;
        private int maxCrystalHearts;

        private bool summitStamp;
        private bool farewellStamp;

        private int totalGoldenStrawberries;
        private int totalHeartGems;
        private int totalCassettes;

        private bool Golden => !Corrupted && Exists && SaveData.TotalStrawberries >= maxStrawberryCountIncludingUntracked;

        public patch_OuiFileSelectSlot(int index, OuiFileSelect fileSelect, SaveData data)
            : base(index, fileSelect, data) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_Show();
        public void Show() {
            // Temporarily set the current save data to the file slot's save data.
            // This enables filtering the areas by the save data's current levelset.
            SaveData prev = SaveData.Instance;
            SaveData.Instance = SaveData;

            LevelSetStats stats = SaveData?.GetLevelSetStats();

            if (stats != null) {
                StrawberriesCounter strawbs = Strawberries;
                strawbs.Amount = stats.TotalStrawberries;
                strawbs.OutOf = stats.MaxStrawberries;
                strawbs.ShowOutOf = stats.Name != "Celeste" || strawbs.OutOf <= 0;
                strawbs.CanWiggle = false;

                if(stats.Name == "Celeste") {
                    // never mess with vanilla.
                    maxStrawberryCount = 175;
                    maxGoldenStrawberryCount = 25;
                    maxStrawberryCountIncludingUntracked = 202;

                    maxCassettes = 8;
                    maxCrystalHeartsExcludingCSides = 16;
                    maxCrystalHearts = 24;

                    summitStamp = SaveData.Areas[7].Modes[0].Completed;
                    farewellStamp = SaveData.Areas[10].Modes[0].Completed;
                } else {
                    // compute the counts for the current level set.
                    maxStrawberryCount = stats.MaxStrawberries;
                    maxGoldenStrawberryCount = stats.MaxGoldenStrawberries;
                    maxStrawberryCountIncludingUntracked = stats.MaxStrawberriesIncludingUntracked;

                    maxCassettes = stats.MaxCassettes;
                    maxCrystalHearts = stats.MaxHeartGems;
                    maxCrystalHeartsExcludingCSides = stats.MaxHeartGemsExcludingCSides;

                    // summit stamp is displayed if we finished all areas that are not interludes. (TotalCompletions filters interludes out.)
                    summitStamp = stats.TotalCompletions >= stats.MaxCompletions;
                    farewellStamp = false; // what is supposed to be Farewell in mod campaigns anyway??
                }

                // save the values from the current level set. They will be patched in instead of SaveData.TotalXX.
                totalGoldenStrawberries = stats.TotalGoldenStrawberries; // The value saved on the file is global for all level sets.
                totalHeartGems = stats.TotalHeartGems; // this counts from all level sets. 
                totalCassettes = stats.TotalCassettes; // this relies on SaveData.Instance.
                
                // redo what is done on the constructor. This keeps the area name and stats up-to-date with the latest area.
                FurthestArea = SaveData.UnlockedAreas;
                Cassettes.Clear();
                HeartGems.Clear();
                foreach (AreaStats areaStats in SaveData.Areas) {
                    if (areaStats.ID > SaveData.UnlockedAreas) break;

                    if (!AreaData.Areas[areaStats.ID].Interlude && AreaData.Areas[areaStats.ID].CanFullClear) {
                        bool[] hearts = new bool[3];
                        for (int i = 0; i < hearts.Length; i++) {
                            hearts[i] = areaStats.Modes[i].HeartGem;
                        }
                        Cassettes.Add(areaStats.Cassette);
                        HeartGems.Add(hearts);
                    }
                }
            }

            SaveData.Instance = prev;

            orig_Show();
        }

        public extern void orig_CreateButtons();
        public new void CreateButtons() {
            orig_CreateButtons();

            if (Everest.Flags.IsDisabled || !CoreModule.Settings.ShowModOptionsInGame)
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
            if (area.GetLevelSet() != "Celeste") {
                // Pretend that we've beaten Prologue.
                LevelSetStats stats = SaveData.Instance.GetLevelSetStatsFor("Celeste");
                stats.UnlockedAreas = 1;
                stats.AreasIncludingCeleste[0].Modes[0].Completed = true;
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


        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchFileSelectSlotRender] // ... except for manually manipulating the method via MonoModRules
        public override extern void Render();
    }
}
