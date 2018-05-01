using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    public class OuiVersionList : Oui {

        private TextMenu menu;

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        private float alpha = 0f;

        private List<TextMenuExt.IItemExt> items = new List<TextMenuExt.IItemExt>();

        private int buildsPerBranch = 4;

        public OuiVersionList() {
        }
        
        public TextMenu CreateMenu(bool inGame, EventInstance snapshot) {
            menu = new TextMenu();
            items.Clear();

            menu.Add(new TextMenu.Header(Dialog.Clean("updater_versions_title")));

            menu.Add(new TextMenu.SubHeader(Dialog.Clean("updater_versions_current").Replace("((version))", Everest.VersionString)));

            ReloadItems();

            return menu;
        }

        private void ReloadItems() {
            foreach (TextMenu.Item item in items)
                menu.Remove(item);
            items.Clear();

            foreach (Everest.Updater.Source source in Everest.Updater.Sources) {
                TextMenuExt.SubHeaderExt header = new TextMenuExt.SubHeaderExt(source.NameDialog.DialogClean());
                header.Alpha = 0f;
                menu.Add(header);
                items.Add(header);

                if (source.ErrorDialog != null) {
                    string text = source.ErrorDialog.DialogClean();
                    TextMenuExt.SubHeaderExt error = new TextMenuExt.SubHeaderExt(text);
                    error.Alpha = 0f;
                    menu.Add(error);
                    items.Add(error);
                    continue;
                }

                if (source.Entries == null) {
                    TextMenuExt.SubHeaderExt info = new TextMenuExt.SubHeaderExt(Dialog.Clean("updater_versions_requesting"));
                    info.Alpha = 0f;
                    menu.Add(info);
                    items.Add(info);
                    continue;
                }

                string branch = null;
                int count = 0;
                foreach (Everest.Updater.Entry entry in source.Entries) {
                    if (entry.Branch != branch) {
                        branch = entry.Branch;
                        count = 0;

                        if (!string.IsNullOrEmpty(entry.Branch)) {
                            TextMenuExt.SubHeaderExt headerBranch = new TextMenuExt.SubHeaderExt("branch: " + entry.Branch);
                            headerBranch.Alpha = 0f;
                            menu.Add(headerBranch);
                            items.Add(headerBranch);
                        }
                    }
                    if (count >= buildsPerBranch)
                        continue;
                    count++;
                    TextMenuExt.ButtonExt item = new TextMenuExt.ButtonExt(entry.Name);
                    item.Alpha = 0f;
                    menu.Add(item.Pressed(() => {
                        Everest.Updater.Update(OuiModOptions.Instance.Overworld.Goto<OuiLoggedProgress>(), entry);
                    }));
                    items.Add(item);
                    continue;
                }

            }

            // Do this afterwards as the menu has now properly updated its size.
            for (int i = 0; i < items.Count; i++)
                Add(new Coroutine(FadeIn(i, items[i])));

            if (menu.Height > menu.ScrollableMinSize) {
                menu.Position.Y = menu.ScrollTargetY;
            }
        }

        private IEnumerator FadeIn(int i, TextMenuExt.IItemExt item) {
            yield return 0.03f * i;
            float ease = 0f;

            Vector2 offset = item.Offset;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                ease = Ease.CubeOut(p);
                item.Alpha = ease;
                item.Offset = offset + new Vector2(0f, 64f * (1f - ease));
                yield return null;
            }

            item.Alpha = 1f;
            item.Offset = offset;
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
        }

        public override IEnumerator Leave(Oui next) {
            Audio.Play(Sfxs.ui_main_whoosh_large_out);
            menu.Focused = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = onScreenX + 1920f * Ease.CubeIn(p);
                alpha = 1f - Ease.CubeIn(p);
                yield return null;
            }

            menu.Visible = Visible = false;
            menu.RemoveSelf();
            menu = null;
        }

        public override void Update() {
            if (menu != null && menu.Focused &&
                Selected && Input.MenuCancel.Pressed) {
                Audio.Play(Sfxs.ui_main_button_back);
                Overworld.Goto<OuiModOptions>();
            }

            base.Update();
        }

        public override void Render() {
            if (alpha > 0f)
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * alpha * 0.4f);
            base.Render();
        }

    }
}
