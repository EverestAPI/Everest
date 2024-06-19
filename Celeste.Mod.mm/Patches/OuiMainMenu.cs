#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste {
    class patch_OuiMainMenu : OuiMainMenu {

        // We're effectively in OuiTitleScreen, but still need to "expose" private fields to our mod.
        private List<MenuButton> buttons;
        public List<MenuButton> Buttons => buttons;
        private MainMenuClimb climbButton;

        public extern void orig_CreateButtons();
        public new void CreateButtons() {
            orig_CreateButtons();

            Everest.Events.MainMenu.CreateButtons(this, buttons);

            UpdateLayout();
        }

        public void RebuildMainAndTitle() {
            Overworld oui = Overworld;
            oui.UIs.Remove(oui.GetUI<OuiTitleScreen>());
            Oui title = new OuiTitleScreen() {
                Visible = false
            };
            title.IsStart(oui, Overworld.StartMode.MainMenu);
            oui.Add(title);
            oui.UIs.Add(title);

            MenuButton selected = null;
            foreach (MenuButton button in buttons) {
                if (!button.Selected)
                    continue;
                selected = button;
                break;
            }

            CreateButtons();

            if (selected is MainMenuClimb) {
                foreach (MenuButton button in buttons) {
                    button.SetSelected(button is MainMenuClimb);
                }
            } else {
                string selectedLabel = (selected as patch_MainMenuSmallButton)?.LabelName;
                foreach (MenuButton button in buttons) {
                    button.SetSelected((button as patch_MainMenuSmallButton)?.LabelName == selectedLabel);
                }
            }
        }

        public void UpdateLayout() {
            UpdateLayout(CoreModule.Settings.MainMenuMode);
        }
        public void UpdateLayout(string mode) {
            foreach (MenuButton button in buttons)
                button.UpButton = button.DownButton = button.LeftButton = button.RightButton = null;

            // TODO: Let mods register custom main menu modes?

            if (mode == "rows") {
                /* CL  111  444
                 * IM  222  555
                 * B-  333  666
                 * 
                 * If there are more than 6 options, shift the start position up and Climb slightly up.
                 */
                int rowCount = Math.Max(3, buttons.Count / 2);
                Vector2 itemSize = new Vector2(600f, 80f);
                Vector2 offsStart = new Vector2(0f, 640f);
                Vector2 startPos = new Vector2(140f, 700f) - new Vector2(0, (rowCount - 3) * itemSize.Y);

                List<MenuButton>[] rows = new List<MenuButton>[rowCount];
                for (int iy = 0; iy < rows.Length; iy++) {
                    rows[iy] = new List<MenuButton>();
                }

                // shift Debug before Credits if we find both of these.
                List<MenuButton> switchedAroundButtons = new List<MenuButton>(buttons);
                int debugOptionIndex = findButtonIndex("menu_debug", "menu/options");
                int creditsOptionIndex = findButtonIndex("menu_credits", "menu/credits");
                if (debugOptionIndex != -1 && creditsOptionIndex != -1) {
                    MenuButton debugButton = switchedAroundButtons[debugOptionIndex];
                    switchedAroundButtons.RemoveAt(debugOptionIndex);
                    if (creditsOptionIndex > debugOptionIndex) {
                        creditsOptionIndex--;
                    }
                    switchedAroundButtons.Insert(creditsOptionIndex, debugButton);
                }

                int x = 0;
                int y = 0;
                for (int i = 0; i < switchedAroundButtons.Count; i++) {
                    MenuButton button = switchedAroundButtons[i];

                    Vector2 pos = startPos + itemSize * new Vector2(x, y);

                    int width = 1;
                    int height = 1;

                    if (button == climbButton) {
                        pos.X += 140f;
                        pos.Y += (rowCount - 3) * itemSize.Y / 2 - 30f;
                        height = rowCount;
                    }

                    for (int iy = y; iy < y + height && iy < rows.Length; iy++) {
                        for (int ix = 0; ix < width; ix++) {
                            rows[iy].Add(button);
                        }
                    }

                    y += height;
                    if (y >= rows.Length) {
                        x++;
                        y = 0;
                    }

                    button.TargetPosition = pos;
                    button.Position = button.TweenFrom = pos + offsStart;
                    if (Visible && Focused)
                        button.Position = button.TargetPosition;

                    if (!Scene.Entities.Contains(button))
                        Scene.Add(button);
                }

                for (int iy = 0; iy < rows.Length; iy++) {
                    List<MenuButton> row = rows[iy];

                    int rowUpY = iy > 0 ? iy - 1 : rows.Length - 1;
                    List<MenuButton> rowUp = rows[rowUpY];

                    List<MenuButton> rowDown = iy < rows.Length - 1 ? rows[iy + 1] : rows[0];

                    for (int ix = 0; ix < row.Count; ix++) {
                        while (rowUp.Count <= ix) {
                            rowUpY--;
                            if (rowUpY < 0)
                                rowUpY += rows.Length;
                            rowUp = rows[rowUpY];
                        }

                        MenuButton button = row[ix];
                        button.LeftButton = button.LeftButton ?? (ix > 0 ? row[ix - 1] : row[row.Count - 1]);
                        button.RightButton = button.RightButton ?? (ix < row.Count - 1 ? row[ix + 1] : row[0]);
                        button.UpButton = button.UpButton ?? (rowUp[ix < rowUp.Count ? ix : (rowUp.Count - 1)]);
                        button.DownButton = button.DownButton ?? (rowDown[ix < rowDown.Count ? ix : (rowDown.Count - 1)]);

                        if (iy != rows.Length - 1 && button is MainMenuModOptionsButton modOptions) {
                            // Mod Options isn't on the last row => make its sub-text closer to prevent overlap.
                            modOptions.SmallSubTextSpacing = true;
                        }
                    }
                }

            } else {
                // Default list. offs is 210px further to the left than vanilla, to account for longer menu text ("an Everest update is available").
                Vector2 pos = new Vector2(320f, 160f);
                Vector2 offs = new Vector2(-850f, 0f);

                for (int i = 0; i < buttons.Count; i++) {
                    MenuButton button = buttons[i];

                    button.UpButton = button.UpButton ?? (i > 0 ? buttons[i - 1] : buttons[buttons.Count - 1]);
                    button.DownButton = button.DownButton ?? (i < buttons.Count - 1 ? buttons[i + 1] : buttons[0]);

                    button.TargetPosition = pos;
                    button.Position = button.TweenFrom = pos + offs;
                    if (Visible && Focused)
                        button.Position = button.TargetPosition;

                    pos += Vector2.UnitY * button.ButtonHeight;
                    if (button == climbButton)
                        pos.X -= 140f;

                    if (!Scene.Entities.Contains(button))
                        Scene.Add(button);
                }
            }

        }

        private int findButtonIndex(string labelName, string iconName) {
            return buttons.FindIndex(_ => {
                patch_MainMenuSmallButton other = (_ as patch_MainMenuSmallButton);
                if (other == null)
                    return false;
                return other.LabelName == labelName && other.IconName == iconName;
            });
        }

        [MonoModReplace]
        private void OnDebug() {
            Audio.Play("event:/ui/main/whoosh_list_out");
            Audio.Play("event:/ui/main/button_select");

            SaveData.InitializeDebugMode(true);

            if (SaveData.Instance.CurrentSession != null && SaveData.Instance.CurrentSession.InArea) {
                Audio.SetMusic(null);
                Audio.SetAmbience(null);
                Overworld.ShowInputUI = false;
                Focused = false;
                new FadeWipe(Scene, false, () => LevelEnter.Go(SaveData.Instance.CurrentSession, true));
                return;
            }

            Overworld.Goto<OuiChapterSelect>();
        }

    }
    public static class OuiMainMenuExt {

        [Obsolete("Use OuiMainMenu.Buttons instead.")]
        public static List<MenuButton> GetButtons(this OuiMainMenu self)
            => ((patch_OuiMainMenu) self).Buttons;

    }
}
