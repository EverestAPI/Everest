using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public class OuiMapList : Oui {

        private TextMenu menu;

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        private float alpha = 0f;

        private int type = 1;
        private int side = 0;

        public OuiMapList() {
        }
        
        public TextMenu CreateMenu(bool inGame, EventInstance snapshot) {
            TextMenu menu = new TextMenu();

            menu.Add(new TextMenu.Header(Dialog.Clean("maplist_title")));

            if (menu.Height > menu.ScrollableMinSize) {
                menu.Position.Y = menu.ScrollTargetY;
            }

            menu.Add(new TextMenu.SubHeader(Dialog.Clean("maplist_filters")));

            // TODO: Various map types? Determine max type.
            menu.Add(new TextMenu.Slider(Dialog.Clean("maplist_type"), value => Dialog.Clean("maplist_type_" + value), 0, 1, type).Change(value => {
                type = value;
                ReloadMenu();
            }));

            menu.Add(new TextMenu.Slider(Dialog.Clean("maplist_side"), value => ((char) ('A' + value)).ToString(), 0, Enum.GetValues(typeof(AreaMode)).Length, side).Change(value => {
                side = value;
                ReloadMenu();
            }));

            menu.Add(new TextMenu.SubHeader(Dialog.Clean("maplist_list")));

            int min = 0;
            int max = AreaData.Areas.Count;
            if (type == 0) {
                max = 10;
            } else {
                min = 10;
            }

            for (int i = min; i < max; i++) {
                AreaData area = AreaData.Areas[i];
                if (!area.HasMode((AreaMode) side))
                    continue;
                string name = area.Name;
                name = name.DialogCleanOrNull() ?? name.SpacedPascalCase();
                menu.Add(new TextMenu.Button(name).Pressed(() => {
                    Start(area, (AreaMode) side);
                }));
            }
            return menu;
        }

        private void ReloadMenu() {
            Vector2 position = Vector2.Zero;

            int selected = -1;
            if (menu != null) {
                position = menu.Position;
                selected = menu.Selection;
                Scene.Remove(menu);
            }

            menu = CreateMenu(false, null);

            if (selected >= 0) {
                menu.Selection = selected;
                menu.Position = position;
            }

            Scene.Add(menu);
        }

        public override IEnumerator Enter(Oui from) {
            ReloadMenu();

            menu.Visible = (Visible = true);
            menu.Focused = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = offScreenX + -1920f * Ease.CubeOut(p);
                alpha = Ease.CubeOut(p);
                yield return null;
            }

            menu.Focused = true;
            yield break;
        }

        public override IEnumerator Leave(Oui next) {
            Audio.Play("event:/ui/main/whoosh_large_out");
            menu.Focused = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = onScreenX + 1920f * Ease.CubeIn(p);
                alpha = 1f - Ease.CubeIn(p);
                yield return null;
            }

            menu.Visible = Visible = false;
            menu.RemoveSelf();
            menu = null;
            yield break;
        }

        public override void Update() {
            if (menu != null && menu.Focused &&
                Selected && Input.MenuCancel.Pressed) {
                Audio.Play("event:/ui/main/button_back");
                Overworld.Goto<OuiChapterSelect>();
            }

            base.Update();
        }

        public override void Render() {
            if (alpha > 0f)
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * alpha * 0.4f);
            base.Render();
        }

        public void Start(AreaData area, AreaMode mode = AreaMode.Normal, string checkpoint = null) {
            Focused = false;
            Audio.Play("event:/ui/world_map/chapter/checkpoint_start");
            Add(new Coroutine(StartRoutine(area, mode, checkpoint), true));
        }

        private IEnumerator StartRoutine(AreaData area, AreaMode mode = AreaMode.Normal, string checkpoint = null) {
            Overworld.Maddy.Hide(false);
            area.Wipe(Overworld, false, null);
            Audio.SetMusic(null, true, true);
            Audio.SetAmbience(null, true);
            if ((area.ID == 0 || area.ID == 9) && checkpoint == null && mode == AreaMode.Normal) {
                Overworld.RendererList.UpdateLists();
                Overworld.RendererList.MoveToFront(Overworld.Snow);
            }
            yield return 0.5f;
            LevelEnter.Go(new Session(area.GetKey(mode), checkpoint), false);
            yield break;
        }

    }
}
