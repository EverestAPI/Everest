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

        private extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            // Note: You may instinctually call base.Added();
            // DON'T! The original method is orig_Added
            orig_Added(scene);

            // Remove any icons having "areas/null" as icon.
            // TODO: Get OuiChapterSelect to handle "holes" properly.
            for (int i = AreaData.Areas.Count - 1; i >= 10; --i) {
                if (AreaData.Areas[i].Icon != "areas/null")
                    continue;
                OuiChapterSelectIcon icon = icons[i];
                // icons.RemoveAt(i);
                Scene.Remove(icon);
            }
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

            orig_Update();

            maplistEase = Calc.Approach(maplistEase, (display && !disableInput && Focused) ? 1f : 0f, Engine.DeltaTime * 4f);
        }

        public extern IEnumerator orig_Enter(Oui from);
        public override IEnumerator Enter(Oui from) {
            // Fix "out of bounds" iconless levels.
            if (area < 0)
                area = 0;
            else if (area > SaveData.Instance.UnlockedAreas)
                area = SaveData.Instance.UnlockedAreas;
            return orig_Enter(from);
        }

        public extern void orig_Render();
        public override void Render() {
            orig_Render();
            if (maplistEase > 0f) {
                Vector2 pos = new Vector2(128f * Ease.CubeOut(maplistEase), 952f);
                if (journalEnabled)
                    pos.Y -= 128f;
                GFX.Gui["menu/maplist"].DrawCentered(pos, Color.White * Ease.CubeOut(maplistEase));
                (Input.GuiInputController() ? Input.GuiButton(Input.Pause) : Input.GuiButton(Input.ESC)).Draw(pos, Vector2.Zero, Color.White * Ease.CubeOut(maplistEase));
            }
        }

    }
}
