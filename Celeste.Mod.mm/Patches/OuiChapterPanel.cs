#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    class patch_OuiChapterPanel : OuiChapterPanel {

        private bool instantClose;
        private List<Option> modes;

        // Make private fields accessible to our mod.
        [MonoModIgnore]
        private List<Option> options { get; set; }
        [MonoModIgnore]
        private int option { get; set; }
        [MonoModIgnore]
        private class Option {
            public string Label;
            public string ID;
            public MTexture Icon;
            public MTexture Bg;
        }

        [MonoModReplace]
        private Option AddRemixButton() {
            Option option = new Option {
                Label = Dialog.Clean("overworld_remix"),
                Icon = GFX.Gui[_ModMenuTexture("menu/remix")],
                ID = "B",
                Bg = GFX.Gui[_ModAreaselectTexture("areaselect/tab")]
            };
            modes.Insert(1, option);
            return option;
        }

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

            bool isStart = orig_IsStart(overworld, start);

            if (isStart && option >= options.Count && options.Count == 1) {
                // we are coming back from a B/C-side and we didn't unlock B-sides. Force-add it.
                AddRemixButton();
            }
            if (isStart && option >= options.Count && options.Count == 2) {
                // we are coming back from a C-side we did not unlock. Force-add it.
                options.Add(new Option {
                    Label = Dialog.Clean("overworld_remix2"),
                    Icon = GFX.Gui[_ModMenuTexture("menu/rmx2")],
                    ID = "C",
                    Bg = GFX.Gui[_ModAreaselectTexture("areaselect/tab")]
                });
            }

            return isStart;
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
            if (Selected && Focused) {
                if (Input.QuickRestart.Pressed) {
                    Overworld.Goto<OuiChapterSelect>();
                    Overworld.Goto<OuiMapSearch>();
                    return;
                }
            }

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
            LevelEnter.Go(new Session(Area, checkpoint), false);
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchOuiChapterPanelRender] // ... except for manually manipulating the method via MonoModRules
        public new extern void Render();

        [MonoModIgnore]
        [PatchOuiChapterPanelOptionBg]
        [PatchOuiChapterPanelReset]
        private extern void Reset();

        private string _ModAreaselectTexture(string textureName) {
            // First, check for area (chapter) specific textures.
            string area = AreaData.Areas[Area.ID].Name;
            string areaTextureName = textureName.Replace("areaselect/", $"areaselect/{area}_");
            if (GFX.Gui.Has(areaTextureName)) {
                textureName = areaTextureName;
                return textureName;
            }

            // If none are found, fall back to levelset textures.
            string levelSet = SaveData.Instance?.GetLevelSet() ?? "Celeste";
            string levelSetTextureName = textureName.Replace("areaselect/", $"areaselect/{levelSet}/");
            if (GFX.Gui.Has(levelSetTextureName)) {
                textureName = levelSetTextureName;
                return textureName;
            }

            // If that doesn't exist either, return without changing anything.
            return textureName;
        }

        private string _ModMenuTexture(string textureName) {
            // First, check for area (chapter) specific textures.
            string area = AreaData.Areas[Area.ID].Name;
            string areaTextureName = textureName.Replace("menu/", $"menu/{area}_");
            if (GFX.Gui.Has(areaTextureName)) {
                textureName = areaTextureName;
                return textureName;
            }

            // If none are found, fall back to levelset textures.
            string levelSet = SaveData.Instance?.GetLevelSet() ?? "Celeste";
            string levelSetTextureName = textureName.Replace("menu/", $"menu/{levelSet}/");
            if (GFX.Gui.Has(levelSetTextureName)) {
                textureName = levelSetTextureName;
                return textureName;
            }

            // If that doesn't exist either, return without changing anything.
            return textureName;
        }

        private float _FixTitleLength(float vanillaValue) {
            float mapNameSize = ActiveFont.Measure(Dialog.Clean(AreaData.Get(Area).Name)).X;
            return vanillaValue - Math.Max(0f, mapNameSize + vanillaValue - 490f);
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the SwapRoutine method in OuiChapterPanel instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchChapterPanelSwapRoutine))]
    class PatchChapterPanelSwapRoutineAttribute : Attribute { }

    /// <summary>
    /// Patches chapter panel rendering to allow for custom chapter cards and banners.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchOuiChapterPanelRender))]
    class PatchOuiChapterPanelRenderAttribute : Attribute { }

    /// <summary>
    /// Patches various methods to customize the chapter panel tabs.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchOuiChapterPanelOptionBg))]
    class PatchOuiChapterPanelOptionBgAttribute : Attribute { }

    /// <summary>
    /// Patches chapter panel tab rendering to allow for custom backpack/cassette icons.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchOuiChapterPanelReset))]
    class PatchOuiChapterPanelResetAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchChapterPanelSwapRoutine(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_GetCheckpoints = method.DeclaringType.FindMethod("System.Collections.Generic.HashSet`1<System.String> _GetCheckpoints(Celeste.SaveData,Celeste.AreaKey)");
            FieldDefinition f_Area = method.DeclaringType.FindField("Area");
            MethodDefinition m_GetStartName = MonoModRule.Modder.FindType("Celeste.AreaData").Resolve().FindMethod("System.String GetStartName(Celeste.AreaKey)");
            TypeDefinition t_OuiChapterPanel = MonoModRule.Modder.FindType("Celeste.OuiChapterPanel").Resolve();
            MethodDefinition m_ModAreaselectTexture = t_OuiChapterPanel.FindMethod("System.String Celeste.OuiChapterPanel::_ModAreaselectTexture(System.String)");

            // The routine is stored in a compiler-generated method.
            method = method.GetEnumeratorMoveNext();

            new ILContext(method).Invoke(il => {
                ILCursor cursor = new ILCursor(il);
                cursor.GotoNext(instr => instr.MatchCallvirt("Celeste.SaveData", "GetCheckpoints"));
                cursor.Next.OpCode = OpCodes.Call;
                cursor.Next.Operand = m_GetCheckpoints;

                cursor.GotoNext(instr => instr.MatchLdstr("overworld_start"));
                cursor.Remove(); // Remove ldstr
                cursor.Remove(); // Remove ldnull
                // Load this.Area
                cursor.Emit(OpCodes.Ldloc_1);
                cursor.Emit(OpCodes.Ldfld, f_Area);
                cursor.Next.Operand = m_GetStartName;

                // wrap "areaselect/" texture paths in _ModAreaselectTexture
                cursor.Index = 0;
                int matches = 0;
                while (cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchLdstr(out string str) && str.StartsWith("areaselect/"))) {
                    // Push chapter panel
                    cursor.Emit(OpCodes.Ldloc_1);
                    // Move after ldstr
                    cursor.Goto(cursor.Next, MoveType.After);
                    // Insert method call to modify the string.
                    cursor.Emit(OpCodes.Call, m_ModAreaselectTexture);
                    matches++;
                }
                if (matches != 2) {
                    throw new Exception("Incorrect number of matches for string starting with \"areaselect/\".");
                }

                // apply patch for changing Option.Bg
                PatchOuiChapterPanelOptionBg(il, null);
            });
        }

        public static void PatchOuiChapterPanelRender(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_ModAreaselectTexture = context.Method.DeclaringType.FindMethod("System.String Celeste.OuiChapterPanel::_ModAreaselectTexture(System.String)");
            MethodDefinition m_FixTitleLength = context.Method.DeclaringType.FindMethod("System.Single Celeste.OuiChapterPanel::_FixTitleLength(System.Single)");

            ILCursor cursor = new ILCursor(context);
            int matches = 0;
            while (cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchLdstr(out string str) && str.StartsWith("areaselect/"))) {
                // Move to before the string is loaded, but before the labels, so we can redirect break targets to a new instruction.
                // Push this.
                cursor.Emit(OpCodes.Ldarg_0);
                // Move after ldstr
                cursor.Goto(cursor.Next, MoveType.After);
                // Insert method call to modify the string.
                cursor.Emit(OpCodes.Call, m_ModAreaselectTexture);
                matches++;
            }
            if (matches != 6) {
                throw new Exception("Incorrect number of matches for string starting with \"areaselect/\".");
            }

            cursor.Index = 0;
            matches = 0;

            // Resize the title if it does not fit.
            while (cursor.TryGotoNext(instr => instr.MatchLdcR4(-60))) {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Index++;
                cursor.Emit(OpCodes.Call, m_FixTitleLength);
                matches++;
            }
            if (matches != 2) {
                throw new Exception("Incorrect number of matches for float -60f.");
            }
        }

        public static void PatchOuiChapterPanelReset(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_ModMenuTexture = context.Method.DeclaringType.FindMethod("System.String Celeste.OuiChapterPanel::_ModMenuTexture(System.String)");

            ILCursor cursor = new ILCursor(context);
            int matches = 0;
            while (cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchLdstr(out string str) && str.StartsWith("menu/"))) {
                // Move to before the string is loaded, but before the labels, so we can redirect break targets to a new instruction.
                // Push this.
                cursor.Emit(OpCodes.Ldarg_0);
                // Move after ldstr
                cursor.Goto(cursor.Next, MoveType.After);
                // Insert method call to modify the string.
                cursor.Emit(OpCodes.Call, m_ModMenuTexture);
                matches++;
            }
            if (matches != 2) {
                throw new Exception("Incorrect number of matches for string starting with \"menu/\".");
            }
        }

        public static void PatchOuiChapterPanelOptionBg(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_Atlas = MonoModRule.Modder.FindType("Monocle.Atlas").Resolve();
            MethodDefinition m_Atlas_GetItem = t_Atlas.FindMethod("Monocle.MTexture Monocle.Atlas::get_Item(System.String)");
            TypeDefinition t_GFX = MonoModRule.Modder.FindType("Celeste.GFX").Resolve();
            FieldDefinition f_Gui = t_GFX.FindField("Gui");
            TypeDefinition t_OuiChapterPanel = MonoModRule.Modder.FindType("Celeste.OuiChapterPanel").Resolve();
            MethodDefinition m_ModAreaselectTexture = t_OuiChapterPanel.FindMethod("System.String Celeste.OuiChapterPanel::_ModAreaselectTexture(System.String)");
            TypeDefinition t_OuiChapterPanel_Option = MonoModRule.Modder.FindType("Celeste.OuiChapterPanel/Option").Resolve();
            FieldDefinition f_Bg = t_OuiChapterPanel_Option.FindField("Bg");

            ILCursor cursor = new ILCursor(context);
            int matches = 0;
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchNewobj("Celeste.OuiChapterPanel/Option"))) {
                // add the following to initializer:
                // Bg = GFX.Gui[_ModAreaselectTexture("areaselect/tab")]
                cursor.Emit(OpCodes.Dup);
                cursor.Emit(OpCodes.Ldsfld, f_Gui);
                // null attribute gets passed from SwapRoutine patch; actual method being patched is MoveNext of nested type, which has the OuiChapterPanel object in local var 1
                cursor.Emit(attrib == null ? OpCodes.Ldloc_1 : OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldstr, "areaselect/tab");
                cursor.Emit(OpCodes.Call, m_ModAreaselectTexture);
                cursor.Emit(OpCodes.Callvirt, m_Atlas_GetItem);
                cursor.Emit(OpCodes.Stfld, f_Bg);

                matches++;
            }
            if (matches != (context.Method.Name == "AddRemixButton" ? 1 : 2)) { // AddRemixButton has 1 instance, the others have 2 each
                throw new Exception($"Incorrect number of matches for \"areaselect/tab\" in {context.Method.Name}.");
            }
        }

    }
}
