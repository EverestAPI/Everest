#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

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

        private bool instantClose = false;

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
        }
        [MonoModIgnore]
        private extern Option AddRemixButton();

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
                    Icon = GFX.Gui["menu/rmx2"],
                    ID = "C"
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

    static partial class MonoModRules {

        public static void PatchChapterPanelSwapRoutine(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_GetCheckpoints = method.DeclaringType.FindMethod("System.Collections.Generic.HashSet`1<System.String> _GetCheckpoints(Celeste.SaveData,Celeste.AreaKey)");
            FieldDefinition f_Area = method.DeclaringType.FindField("Area");
            MethodDefinition m_GetStartName = MonoModRule.Modder.FindType("Celeste.AreaData").Resolve().FindMethod("System.String GetStartName(Celeste.AreaKey)");

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

    }
}
