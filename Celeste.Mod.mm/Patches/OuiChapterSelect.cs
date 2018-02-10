#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
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
    class patch_OuiChapterSelect : OuiChapterSelect {

        // We're effectively in OuiTitleScreen, but still need to "expose" private fields to our mod.
        [MonoModIgnore] // This property defines its own getter and setter - don't accidentally replace them.
        private int area { get; set; }
        private List<OuiChapterSelectIcon> icons;
        private int indexToSnap;
        private const int scarfSegmentSize = 2; // We can't change consts.
        private MTexture scarf;
        private MTexture[] scarfSegments;
        private float ease;
        private float journallEase;
        private bool journalEnabled;
        private bool disableInput;
        private bool display;
        private float inputDelay;

        private float maplistEase;
        private float levelsetEase;
        private string currentLevelSet;

        private extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            // Note: You may instinctually call base.Added();
            // DON'T! The original method is orig_Added
            orig_Added(scene);

            // Do we even need to do anything here?
        }

        [MonoModIgnore]
        private extern void EaseCamera();

        [MonoModReplace]
        public override IEnumerator Enter(Oui from) {
            // Fix "out of bounds" level selection.
            int areaOffs = SaveData.Instance.GetLevelSetStats().AreaOffset;
            int areaMax = areaOffs + SaveData.Instance.GetLevelSetStats().UnlockedAreas;
            area = Calc.Clamp(area, areaOffs, areaMax);

            Visible = true;
            EaseCamera();
            display = true;

            journalEnabled = Celeste.PlayMode == Celeste.PlayModes.Debug || (SaveData.Instance?.CheatMode ?? false);
            for (int i = 0; i <= SaveData.Instance.UnlockedAreas && !journalEnabled; i++)
                if (SaveData.Instance.Areas[i].Modes[0].TimePlayed > 0L && !AreaData.Get(i).Interlude)
                    journalEnabled = true;

            OuiChapterSelectIcon unselected = null;
            if (from is OuiChapterPanel)
                (unselected = icons[area]).Unselect();

            currentLevelSet = SaveData.Instance?.GetLevelSet() ?? "Celeste";
            foreach (OuiChapterSelectIcon icon in icons) {
                AreaData area = AreaData.Areas[icon.Area];
                if (area.GetLevelSet() != currentLevelSet)
                    continue;

                int relativeIndex = area.ToKey().GetRelativeIndex() - 1;
                if (relativeIndex <= Math.Max(1, SaveData.Instance.UnlockedAreas) && icon != unselected) {
                    icon.Position = icon.HiddenPosition;
                    icon.Show();
                    icon.AssistModeUnlockable = false;
                } else if (SaveData.Instance.AssistMode && relativeIndex == SaveData.Instance.UnlockedAreas + 1) {
                    icon.Position = icon.HiddenPosition;
                    icon.Show();
                    icon.AssistModeUnlockable = true;
                }

                // yield return 0.01f; // Originally returns 0.01f
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

            foreach (OuiChapterSelectIcon icon in icons) {
                if (selected != icon) {
                    icon.Hide();
                }
                // yield return 0.01f; // Originally returns 0.01f
            }

            Visible = false;
            yield break;
        }

        public extern void orig_Update();
        public override void Update() {
            // Note: You may instinctually call base.Update();
            // DON'T! The original method is orig_Update
            if (Focused && !disableInput && display && (Input.Pause.Pressed || Input.ESC.Pressed)) {
                Overworld.Maddy.Hide(true);
                Audio.Play("event:/ui/main/button_select");
                Audio.Play("event:/ui/main/whoosh_large_in");
                OuiMapList list = Overworld.Goto<OuiMapList>();
                list.OuiIcons = icons;
                return;
            }

            if (Focused && display && !disableInput && inputDelay <= 0f) {
                if (Input.MenuUp.Pressed) {
                    Overworld.Goto<OuiHelper_ChapterSelect_LevelSet>().Direction = -1;
                    return;
                }
                if (Input.MenuDown.Pressed) {
                    Overworld.Goto<OuiHelper_ChapterSelect_LevelSet>().Direction = +1;
                    return;
                }
                // We don't want to copy the entire Update method, but still prevent the option from going out of bounds.
                if (Input.MenuLeft.Pressed &&
                    (area > 0) &&
                    icons[area - 1].IsHidden()
                ) {
                    return;
                }
                if (Input.MenuRight.Pressed &&
                    (area < SaveData.Instance.UnlockedAreas || (SaveData.Instance.AssistMode && area == SaveData.Instance.UnlockedAreas && area < SaveData.Instance.MaxArea)) &&
                    icons[area + 1].IsHidden()
                ) {
                    return;
                }
            }

            orig_Update();

            maplistEase = Calc.Approach(maplistEase, (display && !disableInput && Focused) ? 1f : 0f, Engine.DeltaTime * 4f);
            levelsetEase = Calc.Approach(levelsetEase, (display && !disableInput && Focused) ? 1f : 0f, Engine.DeltaTime * 4f);
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

            if (levelsetEase > 0f) {
                Vector2 pos = new Vector2(1920f - 64f * Ease.CubeOut(maplistEase), 1080f - 128f);
                string line = DialogExt.CleanLevelSet(currentLevelSet);
                ActiveFont.DrawOutline(line, pos, new Vector2(1f, 0.5f), Vector2.One * 0.7f, Color.White * Ease.CubeOut(maplistEase), 2f, Color.Black * Ease.CubeOut(maplistEase));
                Vector2 lineSize = ActiveFont.Measure(line) * 0.7f;
                Input.GuiDirection(new Vector2(0f, -1f)).DrawCentered(pos + new Vector2(-lineSize.X * 0.5f, -lineSize.Y * 0.5f - 16f), Color.White * Ease.CubeOut(maplistEase), 0.5f);
                Input.GuiDirection(new Vector2(0f, +1f)).DrawCentered(pos + new Vector2(-lineSize.X * 0.5f, +lineSize.Y * 0.5f + 16f), Color.White * Ease.CubeOut(maplistEase), 0.5f);
            }
        }

    }
}
