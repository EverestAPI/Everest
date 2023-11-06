#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Celeste {
    class patch_OuiChapterSelect : OuiChapterSelect {

        // We're effectively in OuiTitleScreen, but still need to "expose" private fields to our mod.
        [MonoModIgnore] // This property defines its own getter and setter - don't accidentally replace them.
        private int area { get; set; }
        private List<patch_OuiChapterSelectIcon> icons;
        private int indexToSnap;
        private const int scarfSegmentSize = 2; // We can't change consts.
        private MTexture scarf;
        private MTexture[] scarfSegments;
        private MTexture levelSetScarf;
        private float ease;
        private float journallEase;
        private bool journalEnabled;
        private bool disableInput;
        private bool display;
        private float inputDelay;

        private float maplistEase;
        private float searchEase;
        private float levelsetEase;
        private string currentLevelSet;

        private KeyboardState _keys;

        private extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            orig_Added(scene);

            SaveData save = SaveData.Instance;
            for (int i = icons.Count - 1; i > -1; --i) {
                OuiChapterSelectIcon icon = icons[i];
                patch_AreaData area = patch_AreaData.Get(icon.Area);

                if (!string.IsNullOrEmpty(area?.Meta?.Parent)) {
                    icons[i].Area = -1;
                    icons[i].Hide();
                    continue;
                }
            }
        }

        private void GetMinMaxArea(out int areaOffs, out int areaMax) {
            int areaOffsRaw = patch_SaveData.Instance.LevelSetStats.AreaOffset;
            int areaMaxRaw = Math.Max(areaOffsRaw, SaveData.Instance.UnlockedAreas);

            do {
                areaOffs = icons.FindIndex(i => i?.Area == areaOffsRaw);
            } while (areaOffs == -1 && ++areaOffsRaw < areaMaxRaw);
            if (areaOffs == -1)
                areaOffs = areaOffsRaw;

            do {
                areaMax = icons.FindLastIndex(i => i?.Area == areaMaxRaw || i.AssistModeUnlockable);
            } while (areaMax == -1 && --areaMaxRaw < areaOffsRaw);
            if (areaMax == -1)
                areaMax = areaMaxRaw;
        }

        [MonoModReplace]
        private void EaseCamera() {
            patch_AreaData areaData = patch_AreaData.Areas[area];
            Overworld.Mountain.EaseCamera(area, areaData.MountainIdle, null, true, areaData.Meta?.Mountain?.Rotate ?? areaData.LevelSet == "Celeste" && area == 10);
            Overworld.Mountain.Model.EaseState(areaData.MountainState);
        }

        public extern bool orig_IsStart(Overworld overworld, Overworld.StartMode start);
        public override bool IsStart(Overworld overworld, Overworld.StartMode start) {
            if (start == Overworld.StartMode.AreaComplete || start == Overworld.StartMode.AreaQuit) {
                patch_AreaData area = patch_AreaData.Get(SaveData.Instance.LastArea.ID);
                area = patch_AreaData.Get(area?.Meta?.Parent) ?? area;
                if (area != null)
                    SaveData.Instance.LastArea.ID = area.ID;
            }

            return orig_IsStart(overworld, start);
        }

        [MonoModReplace]
        public override IEnumerator Enter(Oui from) {
            // Fix "out of bounds" level selection.
            GetMinMaxArea(out int areaOffs, out int areaMax);
            int areaUnclamp = area;
            area = Calc.Clamp(area, areaOffs, areaMax);

            Visible = true;
            EaseCamera();
            display = true;

            currentLevelSet = patch_SaveData.Instance?.LevelSet ?? "Celeste";

            journalEnabled = string.IsNullOrEmpty(currentLevelSet) || Celeste.PlayMode == Celeste.PlayModes.Debug || (SaveData.Instance?.CheatMode ?? false);
            for (int i = 0; i <= SaveData.Instance.UnlockedAreas && !journalEnabled; i++)
                if (SaveData.Instance.Areas[i].Modes[0].TimePlayed > 0L && !AreaData.Get(i).Interlude)
                    journalEnabled = true;

            OuiChapterSelectIcon unselected = null;
            if (from is OuiChapterPanel) {
                (unselected = icons[areaUnclamp]).Unselect();
                if (areaUnclamp != area)
                    unselected.Hide();
            }

            levelSetScarf = GFX.Gui.GetOrDefault("areas/" + currentLevelSet + "/hover", GFX.Gui["areas/hover"]);
            updateScarf();

            bool isVanilla = currentLevelSet == "Celeste";
            foreach (OuiChapterSelectIcon icon in icons) {
                patch_AreaData area = patch_AreaData.Get(icon.Area);
                if (area == null || area.LevelSet != currentLevelSet)
                    continue;

                int index = area.ToKey().ID;
                if ((string.IsNullOrEmpty(currentLevelSet) || index <= Math.Max(1, SaveData.Instance.UnlockedAreas))
                    && icon != unselected) {
                    icon.Position = icon.HiddenPosition;
                    icon.Show();
                    icon.AssistModeUnlockable = false;
                } else if (SaveData.Instance.AssistMode && index == SaveData.Instance.UnlockedAreas + 1) {
                    icon.Position = icon.HiddenPosition;
                    icon.Show();
                    icon.AssistModeUnlockable = true;
                }

                if (isVanilla)
                    yield return 0.01f;
            }

            if (from is OuiChapterPanel)
                yield return 0.25f;
        }

        [MonoModReplace]
        private IEnumerator EaseOut(Oui next) {
            OuiChapterSelectIcon selected = null;
            if (next is OuiChapterPanel) {
                (selected = icons[area]).Select();
            }

            bool isVanilla = currentLevelSet == "Celeste";
            foreach (OuiChapterSelectIcon icon in icons) {
                patch_AreaData area = patch_AreaData.Get(icon.Area);
                if (area == null || area.LevelSet != currentLevelSet)
                    continue;

                if (selected != icon)
                    icon.Hide();

                if (isVanilla)
                    yield return 0.01f;
            }

            Visible = false;
            yield break;
        }

        public extern void orig_Update();
        public override void Update() {
            // Note: You may instinctually call base.Update();
            // DON'T! The original method is orig_Update

            KeyboardState keysPrev = _keys;
            KeyboardState keys = Keyboard.GetState();
            _keys = keys;

            if (Focused && !disableInput && display) {
                if (Input.Pause.Pressed || Input.ESC.Pressed) {
                    Overworld.Maddy.Hide(true);
                    Audio.Play(SFX.ui_main_button_select);
                    Audio.Play(SFX.ui_main_whoosh_large_in);
                    OuiMapList list = Overworld.Goto<OuiMapList>();
                    list.OuiIcons = icons;
                    return;
                } else if (Input.QuickRestart.Pressed) {
                    Overworld.Maddy.Hide(true);
                    Audio.Play(SFX.ui_main_button_select);
                    Audio.Play(SFX.ui_main_whoosh_large_in);
                    OuiMapSearch list = Overworld.Goto<OuiMapSearch>();
                    list.OuiIcons = icons;
                    return;
                }
            }

            // note: Engine.DeltaTime is removed from inputDelay before being compared to zero in the orig method.
            if (Focused && display && !disableInput && inputDelay <= Engine.DeltaTime) {
                if (Input.MenuUp.Pressed) {
                    Audio.Play(SFX.ui_world_chapter_pane_contract);
                    Audio.Play(SFX.ui_world_icon_roll_left);
                    Overworld.Goto<OuiHelper_ChapterSelect_LevelSet>().Direction = -1;
                    return;
                }
                if (Input.MenuDown.Pressed) {
                    Audio.Play(SFX.ui_world_chapter_pane_expand);
                    Audio.Play(SFX.ui_world_icon_roll_right);
                    Overworld.Goto<OuiHelper_ChapterSelect_LevelSet>().Direction = +1;
                    return;
                }

                // We don't want to copy the entire Update method, but still prevent the option from going out of bounds.
                GetMinMaxArea(out int areaOffs, out int areaMax);
                if (area < areaOffs) {
                    area = areaOffs;
                } else {
                    if (area > areaMax) {
                        area = areaMax;
                    }
                    while (area > 0 && icons[area].IsHidden) {
                        area--;
                    }
                }

                if (Input.MenuLeft.Pressed && (area - 1 < 0 || icons[area - 1].IsHidden)) {
                    return;
                }

                if (Input.MenuRight.Pressed && (area + 1 >= icons.Count || icons[area + 1].IsHidden)) {
                    return;
                }
            }

            orig_Update();

            if (Focused && display) {
                updateScarf();
            }

            maplistEase = Calc.Approach(maplistEase, (display && !disableInput && Focused) ? 1f : 0f, Engine.DeltaTime * 4f);
            searchEase = Calc.Approach(searchEase, (display && !disableInput && Focused) ? 1f : 0f, Engine.DeltaTime * 4f);
            levelsetEase = Calc.Approach(levelsetEase, (display && !disableInput && Focused) ? 1f : 0f, Engine.DeltaTime * 4f);
        }

        private void updateScarf() {
            string nextScarf = "areas/" + AreaData.Areas[area].Name.ToLowerInvariant() + "_hover";
            if (!nextScarf.Equals(scarf.AtlasPath)) {
                scarf = GFX.Gui.GetOrDefault(nextScarf, levelSetScarf);
                scarfSegments = new MTexture[scarf.Height / 2];
                for (int j = 0; j < scarfSegments.Length; j++) {
                    scarfSegments[j] = scarf.GetSubtexture(0, j * 2, scarf.Width, 2, null);
                }
            }
        }

        public extern void orig_Render();
        public override void Render() {

            orig_Render();
            if (maplistEase > 0f) {
                Vector2 pos = new Vector2(128f * Ease.CubeOut(maplistEase), 1080f - 128f);
                if (journalEnabled)
                    pos.Y -= 128f;
                GFX.Gui["menu/maplist"].DrawCentered(pos, Color.White * Ease.CubeOut(maplistEase));
                (Input.GuiInputController() ? Input.GuiButton(Input.Pause) : Input.GuiButton(Input.ESC)).Draw(pos, Vector2.Zero, Color.White * Ease.CubeOut(maplistEase));
            }

            if (searchEase > 0f) {
                Vector2 pos = new Vector2(128f * Ease.CubeOut(searchEase), 1080f - 128f);
                if (journalEnabled) {
                    pos.Y -= 256f;
                } else {
                    pos.Y -= 128f;
                }
                GFX.Gui["menu/mapsearch"].DrawCentered(pos, Color.White * Ease.CubeOut(searchEase));
                Input.GuiKey(Input.FirstKey(Input.QuickRestart)).Draw(pos, Vector2.Zero, Color.White * Ease.CubeOut(searchEase));
            }

            if (levelsetEase > 0f) {
                Vector2 pos = new Vector2(1920f - 64f * Ease.CubeOut(maplistEase), 1080f - 128f);
                string line = patch_Dialog.CleanLevelSet(currentLevelSet);
                ActiveFont.DrawOutline(line, pos, new Vector2(1f, 0.5f), Vector2.One * 0.7f, Color.White * Ease.CubeOut(maplistEase), 2f, Color.Black * Ease.CubeOut(maplistEase));
                Vector2 lineSize = ActiveFont.Measure(line) * 0.7f;
                Input.GuiDirection(new Vector2(0f, -1f)).DrawCentered(pos + new Vector2(-lineSize.X * 0.5f, -lineSize.Y * 0.5f - 16f), Color.White * Ease.CubeOut(maplistEase), 0.5f);
                Input.GuiDirection(new Vector2(0f, +1f)).DrawCentered(pos + new Vector2(-lineSize.X * 0.5f, +lineSize.Y * 0.5f + 16f), Color.White * Ease.CubeOut(maplistEase), 0.5f);
            }
        }

    }
}
