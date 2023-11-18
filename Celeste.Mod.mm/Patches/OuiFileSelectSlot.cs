#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0414 // The field is assigned but its value is never used

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Celeste {
    public class patch_OuiFileSelectSlot : OuiFileSelectSlot {

        /// <summary>
        /// Interface used to tag OuiFileSelectSlot submenus.
        /// </summary>
        public interface ISubmenu { }

        // We're effectively in OuiFileSelectSlot, but still need to "expose" private fields to our mod.
        public new patch_SaveData SaveData;
        private OuiFileSelect fileSelect;
        private List<Button> buttons;
        private Tween tween;
        private float inputDelay;
        private bool deleting;
        private int buttonIndex;
        private float selectedEase;
        private float newgameFade;
        private Wiggler wiggler;

        [MonoModIgnore]
        private bool selected { get; set; }

        private OuiFileSelectSlotLevelSetPicker newGameLevelSetPicker;

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

        private bool renamed;

        private bool Golden => !Corrupted && Exists && SaveData.TotalStrawberries >= maxStrawberryCountIncludingUntracked;

        // vanilla: new Vector2(960f, 540 + 310 * (FileSlot - 1)); => slot 1 is centered at all times
        // if there are 6 slots (0-based): slot 1 should be centered if slot 0 is selected; slot 4 should be centered if slot 5 is selected; the selected slot should be centered otherwise.
        // this formula doesn't change the behavior with 3 slots, since the slot index will be clamped between 1 and 1.
        public new Vector2 IdlePosition {
            [MonoModReplace]
            get => new Vector2(960f, 540 + 310 * (FileSlot - Calc.Clamp(fileSelect.SlotIndex, 1, fileSelect.Slots.Length - 2)));
        }

        public patch_OuiFileSelectSlot(int index, OuiFileSelect fileSelect, SaveData data)
            : base(index, fileSelect, data) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModConstructor]
        [MonoModIgnore] // don't change anything in the method...
        [PatchTotalHeartGemChecks] // except for replacing TotalHeartGems with TotalHeartGemsInVanilla through MonoModRules
        public extern void ctor(int index, OuiFileSelect fileSelect, SaveData data);

        public extern void orig_Show();
        public new void Show() {
            // Temporarily set the current save data to the file slot's save data.
            // This enables filtering the areas by the save data's current levelset.
            patch_SaveData prev = patch_SaveData.Instance;
            patch_SaveData.Instance = SaveData;

            LevelSetStats stats = SaveData?.LevelSetStats;

            if (stats != null) {
                StrawberriesCounter strawbs = Strawberries;
                strawbs.Amount = stats.TotalStrawberries;
                strawbs.OutOf = stats.MaxStrawberries;
                strawbs.ShowOutOf = stats.Name != "Celeste" || strawbs.OutOf <= 0;
                strawbs.CanWiggle = false;

                if (stats.Name == "Celeste") {
                    // never mess with vanilla.
                    maxStrawberryCount = 175;
                    maxGoldenStrawberryCount = 25; // vanilla is wrong (there are 26 including dashless), but don't mess with vanilla.
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
                    if (areaStats.ID > SaveData.UnlockedAreas)
                        break;

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

            patch_SaveData.Instance = prev;

            orig_Show();
        }

        public extern void orig_CreateButtons();
        public new void CreateButtons() {
            orig_CreateButtons();

            if (!Exists) {
                if (patch_AreaData.Areas.Select(area => area.LevelSet).Distinct().Count() > 1) {
                    if (newGameLevelSetPicker == null) {
                        newGameLevelSetPicker = new OuiFileSelectSlotLevelSetPicker(this);
                    }
                    buttons.Add(newGameLevelSetPicker);
                }
            } else if (!Corrupted) {
                buttons.Insert(buttons.FindIndex(button => button.Label == Dialog.Clean("file_delete")), // Insert immediately before "Delete"
                    new Button {
                        Label = Dialog.Clean("file_rename"),
                        Action = OnExistingFileRenameSelected,
                        Scale = 0.7f
                    }
                );
            }

            patch_SaveData.LoadModSaveData(FileSlot);
            Everest.Events.FileSelectSlot.HandleCreateButtons(buttons, this, Exists);
        }

        private void OnExistingFileRenameSelected() {
            renamed = true;
            Renaming = true;
            OuiFileNaming ouiFileNaming = fileSelect.Overworld.Goto<OuiFileNaming>();
            ouiFileNaming.FileSlot = this;
            ouiFileNaming.StartingName = Name;
            Audio.Play("event:/ui/main/savefile_rename_start");
        }

        [MonoModIgnore]
        [PatchOuiFileSelectSlotOnContinueSelected]
        private extern void OnContinueSelected();

        public extern void orig_OnNewGameSelected();
        public void OnNewGameSelected() {
            patch_SaveData.TryDeleteModSaveData(FileSlot);

            orig_OnNewGameSelected();

            string newGameLevelSet = newGameLevelSetPicker?.NewGameLevelSet;
            if (newGameLevelSet != null && newGameLevelSet != "Celeste") {
                patch_SaveData.Instance.LastArea =
                    patch_AreaData.Areas.FirstOrDefault(area => area.LevelSet == newGameLevelSet)?.ToKey() ??
                    AreaKey.Default;
            }
        }

        public extern void orig_Update();
        public override void Update() {
            orig_Update();

            if (newGameLevelSetPicker != null && selected && fileSelect.Selected && fileSelect.Focused &&
                !StartingGame && tween == null && inputDelay <= 0f && !StartingGame && !deleting) {

                // currently highlighted option is the level set picker, call its Update() method to handle Left and Right presses.
                newGameLevelSetPicker.Update(buttons[buttonIndex] == newGameLevelSetPicker);

                if (MInput.Keyboard.Check(Keys.LeftControl) && MInput.Keyboard.Pressed(Keys.S)) {
                    // Ctrl+S: change the default starting level set to the currently selected one.
                    CoreModule.Settings.DefaultStartingLevelSet = newGameLevelSetPicker.NewGameLevelSet;
                    if (CoreModule.Settings.SaveDataFlush ?? false)
                        CoreModule.Instance.ForceSaveDataFlush++;
                    CoreModule.Instance.SaveSettings();
                    Audio.Play("event:/new_content/ui/rename_entry_accept_locked");
                }
            }
        }

        public void WiggleMenu() {
            wiggler.Start();
        }

        [MonoModReplace]
        private IEnumerator EnterFirstAreaRoutine() {
            ((patch_OuiFileSelect) fileSelect).startingNewFile = true; // Set this flag for autosplitters

            // Replace ID 0 with SaveData.Instance.LastArea.ID

            Overworld overworld = fileSelect.Overworld;
            patch_AreaData area = patch_AreaData.Areas[patch_SaveData.Instance.LastArea.ID];
            if (area.LevelSet != "Celeste") {
                // Pretend that we've beaten Prologue.
                LevelSetStats stats = patch_SaveData.Instance.GetLevelSetStatsFor("Celeste");
                stats.UnlockedAreas = 1;
                stats.AreasIncludingCeleste[0].Modes[0].Completed = true;
            }

            yield return fileSelect.Leave(null);

            overworld.Mountain.Model.EaseState(area.MountainState);
            yield return overworld.Mountain.EaseCamera(0, area.MountainIdle);
            yield return 0.3f;

            overworld.Mountain.EaseCamera(0, area.MountainZoom, 1f);
            yield return 0.4f;

            area.Wipe(overworld, false, null);
            ((patch_RendererList) (object) overworld.RendererList).UpdateLists();
            overworld.RendererList.MoveToFront(overworld.Snow);

            yield return 0.5f;

            LevelEnter.Go(new Session(patch_SaveData.Instance.LastArea), false);
        }

        public extern void orig_Unselect();
        public new void Unselect() {
            orig_Unselect();

            // reset the level set picker when we exit out of the file select slot.
            newGameLevelSetPicker = null;
        }

        // Required because Button is private. Also make it public.
        [MonoModPublic]
        public class Button {
            public string Label;
            public Action Action;
            public float Scale = 1f;
        }

        [PatchFileSelectSlotRender] // manually manipulate the method via MonoModRules
        public extern void orig_Render();
        public override void Render() {
            orig_Render();

            if (selectedEase > 0f) {
                Vector2 position = Position + new Vector2(0f, -150f + 350f * selectedEase);
                float lineHeight = ActiveFont.LineHeight;

                // go through all buttons, looking for the level set picker.
                for (int i = 0; i < buttons.Count; i++) {
                    Button button = buttons[i];
                    if (button == newGameLevelSetPicker) {
                        // we found it: call its Render method.
                        newGameLevelSetPicker.Render(position, buttonIndex == i && !deleting, wiggler.Value * 8f);
                    }
                    position.Y += lineHeight * button.Scale + 15f;
                }
            }
        }

        // very similar to MoveTo, except the easing is different if the slot was already moving.
        // used for scrolling, since using MoveTo can look weird if holding up or down in file select.
        internal void ScrollTo(float x, float y) {
            Vector2 from = Position;
            Vector2 to = new Vector2(x, y);

            bool tweenWasPresent = false;
            if (tween != null && tween.Entity == this) {
                tweenWasPresent = true;
                tween.RemoveSelf();

                // snap the "unselect" animation.
                newgameFade = selectedEase = 0f;
            }
            Add(tween = Tween.Create(Tween.TweenMode.Oneshot, tweenWasPresent ? Ease.CubeOut : Ease.CubeInOut, 0.25f));
            tween.OnUpdate = t => Position = Vector2.Lerp(from, to, t.Eased);
            tween.OnComplete = t => tween = null;
            tween.Start();
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// IL-patch the Render method for file select slots instead of reimplementing it,
    /// to un-hardcode stamps.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchFileSelectSlotRender))]
    class PatchFileSelectSlotRenderAttribute : Attribute { }

    /// <summary>
    /// Patches the method to update the Name and the TheoSisterName, if the file has been renamed in-game, in the file's SaveData.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchOuiFileSelectSlotOnContinueSelected))]
    class PatchOuiFileSelectSlotOnContinueSelectedAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchFileSelectSlotRender(ILContext context, CustomAttribute attrib) {
            TypeDefinition declaringType = context.Method.DeclaringType;
            FieldDefinition f_maxStrawberryCount = declaringType.FindField("maxStrawberryCount");
            FieldDefinition f_maxGoldenStrawberryCount = declaringType.FindField("maxGoldenStrawberryCount");
            FieldDefinition f_maxCassettes = declaringType.FindField("maxCassettes");
            FieldDefinition f_maxCrystalHeartsExcludingCSides = declaringType.FindField("maxCrystalHeartsExcludingCSides");
            FieldDefinition f_maxCrystalHearts = declaringType.FindField("maxCrystalHearts");
            FieldDefinition f_summitStamp = declaringType.FindField("summitStamp");
            FieldDefinition f_farewellStamp = declaringType.FindField("farewellStamp");
            FieldDefinition f_totalGoldenStrawberries = declaringType.FindField("totalGoldenStrawberries");
            FieldDefinition f_totalHeartGems = declaringType.FindField("totalHeartGems");
            FieldDefinition f_totalCassettes = declaringType.FindField("totalCassettes");

            ILCursor cursor = new ILCursor(context);
            // SaveData.TotalStrawberries replaced by SaveData.TotalStrawberries_Safe with MonoModLinkFrom
            // Replace hardcoded ARB value with a field reference
            cursor.GotoNext(MoveType.After, instr => instr.MatchLdcI4(175));
            cursor.Prev.OpCode = OpCodes.Ldarg_0;
            cursor.Emit(OpCodes.Ldfld, f_maxStrawberryCount); // SaveData.Areas replaced by SaveData.Areas_Safe with MonoModLinkFrom
            // We want to replace `this.SaveData.Areas_Safe[7].Modes[0].Completed`
            cursor.GotoNext(instr => instr.MatchLdfld(declaringType.FullName, "SaveData"),
                instr => instr.MatchCallvirt("Celeste.SaveData", "get_Areas_Safe"),
                instr => instr.OpCode == OpCodes.Ldc_I4_7);
            // Remove everything but the preceeding `this`
            cursor.RemoveRange(8);
            // Replace with `this.summitStamp`
            cursor.Emit(OpCodes.Ldfld, f_summitStamp);

            cursor.GotoNext(instr => instr.MatchLdfld(declaringType.FullName, "SaveData"),
                instr => instr.MatchCallvirt("Celeste.SaveData", "get_TotalCassettes"));
            cursor.RemoveRange(3);
            cursor.Emit(OpCodes.Ldfld, f_totalCassettes);
            // Replace hardcoded Cassettes value with a field reference
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_maxCassettes);

            cursor.GotoNext(instr => instr.MatchLdfld(declaringType.FullName, "SaveData"),
                instr => instr.MatchCallvirt("Celeste.SaveData", "get_TotalHeartGems"));
            cursor.RemoveRange(3);
            cursor.Emit(OpCodes.Ldfld, f_totalHeartGems);
            // Replace hardcoded HeartGems value with a field reference
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_maxCrystalHeartsExcludingCSides);

            cursor.GotoNext(instr => instr.MatchLdfld(declaringType.FullName, "SaveData"),
                instr => instr.MatchLdfld("Celeste.SaveData", "TotalGoldenStrawberries"));
            cursor.RemoveRange(3);
            cursor.Emit(OpCodes.Ldfld, f_totalGoldenStrawberries);
            // Replace hardcoded GoldenStrawberries value with a field reference
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_maxGoldenStrawberryCount);

            cursor.GotoNext(instr => instr.MatchLdfld(declaringType.FullName, "SaveData"),
                instr => instr.MatchCallvirt("Celeste.SaveData", "get_TotalHeartGems"));
            cursor.RemoveRange(3);
            cursor.Emit(OpCodes.Ldfld, f_totalHeartGems);
            // Replace hardcoded HeartGems value with a field reference
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_maxCrystalHearts);

            // SaveData.Areas replaced by SaveData.Areas_Safe with MonoModLinkFrom
            // We want to replace `this.SaveData.Areas_Safe[10].Modes[0].Completed`
            cursor.GotoNext(instr => instr.MatchLdfld(declaringType.FullName, "SaveData"),
                instr => instr.MatchCallvirt("Celeste.SaveData", "get_Areas_Safe"),
                instr => instr.MatchLdcI4(10));
            // Remove everything but the preceeding `this`
            cursor.RemoveRange(8);
            // Replace with `this.farewellStamp`
            cursor.Emit(OpCodes.Ldfld, f_farewellStamp);
        }

        public static void PatchOuiFileSelectSlotOnContinueSelected(ILContext context, CustomAttribute attrib) {
            FieldDefinition f_OuiFileSelectSlot_Name = context.Method.DeclaringType.FindField("Name");
            FieldDefinition f_OuiFileSelectSlot_renamed = context.Method.DeclaringType.FindField("renamed");
            FieldDefinition f_SaveData_Name = context.Module.GetType("Celeste.SaveData").Resolve().FindField("Name");
            FieldDefinition f_SaveData_TheoSisterName = context.Module.GetType("Celeste.SaveData").Resolve().FindField("TheoSisterName");
            MethodDefinition m_Dialog_Clean = context.Module.GetType("Celeste.Dialog").Resolve().FindMethod("System.String Clean(System.String,Celeste.Language)");
            TypeDefinition t_String = MonoModRule.Modder.FindType("System.String").Resolve();
            MethodReference m_String_IndexOf = MonoModRule.Modder.Module.ImportReference(t_String.FindMethod("System.Int32 IndexOf(System.String,System.StringComparison)"));

            // Insert after SaveData.Start(SaveData, FileSlot)
            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(MoveType.After, instr => instr.MatchCall("Celeste.SaveData", "System.Void Start(Celeste.SaveData,System.Int32)"));

            // if (renamed)
            // {
            //     SaveData.Instance.Name = Name;
            //     SaveData.Instance.TheoSisterName = Dialog.Clean((Name.IndexOf(Dialog.Clean("THEO_SISTER_NAME"), StringComparison.InvariantCultureIgnoreCase) >= 0) ? "THEO_SISTER_ALT_NAME" : "THEO_SISTER_NAME");
            // }
            ILLabel renamedTarget = cursor.DefineLabel();
            ILLabel altNameTarget = cursor.DefineLabel();
            ILLabel defaultNameTarget = cursor.DefineLabel();

            // if (renamed)
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_OuiFileSelectSlot_renamed);
            cursor.Emit(OpCodes.Brfalse_S, renamedTarget);

            // Assign Name
            cursor.Emit(cursor.Next.OpCode, cursor.Next.Operand); // ldsfld class Celeste.SaveData Celeste.SaveData::Instance
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_OuiFileSelectSlot_Name);
            cursor.Emit(OpCodes.Stfld, f_SaveData_Name);

            // Assign TheoSisterName
            cursor.Emit(cursor.Next.OpCode, cursor.Next.Operand); // ldsfld class Celeste.SaveData Celeste.SaveData::Instance
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_OuiFileSelectSlot_Name);
            cursor.Emit(OpCodes.Ldstr, "THEO_SISTER_NAME");
            cursor.Emit(OpCodes.Ldnull);
            cursor.Emit(OpCodes.Call, m_Dialog_Clean);
            cursor.Emit(OpCodes.Ldc_I4_3);
            cursor.Emit(OpCodes.Callvirt, m_String_IndexOf);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Bge_S, altNameTarget);
            cursor.Emit(OpCodes.Ldstr, "THEO_SISTER_NAME");
            cursor.Emit(OpCodes.Br_S, defaultNameTarget);
            cursor.MarkLabel(altNameTarget);
            cursor.Emit(OpCodes.Ldstr, "THEO_SISTER_ALT_NAME");
            cursor.MarkLabel(defaultNameTarget);
            cursor.Emit(OpCodes.Ldnull);
            cursor.Emit(OpCodes.Call, m_Dialog_Clean);
            cursor.Emit(OpCodes.Stfld, f_SaveData_TheoSisterName);

            // Target for if renamed is false
            cursor.MarkLabel(renamedTarget);
        }

    }
}
