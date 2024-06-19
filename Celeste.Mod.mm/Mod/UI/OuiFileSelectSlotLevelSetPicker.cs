using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace Celeste.Mod.UI {
    class OuiFileSelectSlotLevelSetPicker : patch_OuiFileSelectSlot.Button {
        public string NewGameLevelSet { get; private set; }

        private OuiFileSelectSlot selectSlot;
        private Vector2 arrowOffset;
        private int lastDirection;

        public OuiFileSelectSlotLevelSetPicker(OuiFileSelectSlot selectSlot) {
            this.selectSlot = selectSlot;

            // if the default starting level set still exists, set it by default.
            if (patch_AreaData.Areas.Any(area => area.LevelSet == CoreModule.Settings.DefaultStartingLevelSet)) {
                NewGameLevelSet = CoreModule.Settings.DefaultStartingLevelSet;
            }

            Label = patch_Dialog.CleanLevelSet(NewGameLevelSet ?? "Celeste");
            Scale = 0.5f;
            Action = () => changeStartingLevelSet(1);

            // find out what is the width of the biggest level set out there.
            float levelSetNameWidth = 0;
            foreach (patch_AreaData areaData in AreaData.Areas) {
                levelSetNameWidth = Math.Max(levelSetNameWidth, ActiveFont.Measure(patch_Dialog.CleanLevelSet(areaData.LevelSet)).X);
            }
            arrowOffset = new Vector2(20f + levelSetNameWidth / 2 * Scale, 0f);
        }

        public void Update(bool selected) {
            if (selected) {
                if (Input.MenuLeft.Pressed) {
                    changeStartingLevelSet(-1);
                } else if (Input.MenuRight.Pressed) {
                    changeStartingLevelSet(1);
                }
            } else {
                lastDirection = 0;
            }
        }

        public void Render(Vector2 position, bool currentlySelected, float wigglerOffset) {
            Vector2 wigglerShift = Vector2.UnitX * (currentlySelected ? wigglerOffset : 0f);
            Color color = selectSlot.SelectionColor(currentlySelected);

            Vector2 leftArrowWigglerShift = lastDirection <= 0 ? wigglerShift : Vector2.Zero;
            Vector2 rightArrowWigglerShift = lastDirection >= 0 ? wigglerShift : Vector2.Zero;

            ActiveFont.DrawOutline("<", position + leftArrowWigglerShift - arrowOffset, new Vector2(0.5f, 0f), Vector2.One * Scale, color, 2f, Color.Black);
            ActiveFont.DrawOutline(">", position + rightArrowWigglerShift + arrowOffset, new Vector2(0.5f, 0f), Vector2.One * Scale, color, 2f, Color.Black);
        }

        private void changeStartingLevelSet(int direction) {
            lastDirection = direction;
            Audio.Play((direction > 0) ? "event:/ui/main/button_toggle_on" : "event:/ui/main/button_toggle_off");

            if (NewGameLevelSet == null)
                NewGameLevelSet = "Celeste";

            int id;
            if (direction > 0) {
                id = patch_AreaData.Areas.FindLastIndex(area => area.LevelSet == NewGameLevelSet) + direction;
            } else {
                id = patch_AreaData.Areas.FindIndex(area => area.LevelSet == NewGameLevelSet) + direction;
            }

            if (id >= AreaData.Areas.Count)
                id = 0;
            if (id < 0)
                id = AreaData.Areas.Count - 1;

            NewGameLevelSet = patch_AreaData.Areas[id].LevelSet;

            Label = patch_Dialog.CleanLevelSet(NewGameLevelSet ?? "Celeste");
            ((patch_OuiFileSelectSlot) selectSlot).WiggleMenu();
        }
    }
}
