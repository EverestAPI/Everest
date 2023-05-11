using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System.Collections;
using System.Collections.Generic;

namespace Celeste.Mod.UI {
    public class OuiVersionList : Oui, OuiModOptions.ISubmenu {

        private TextMenu menu;

        private TextMenu.SubHeader currentBranchName;

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        private float alpha = 0f;

        private List<TextMenuExt.IItemExt> items = new List<TextMenuExt.IItemExt>();

        private int buildsPerBranch = 12;

        public OuiVersionList() {
        }

        public TextMenu CreateMenu(bool inGame, EventInstance snapshot) {
            menu = new patch_TextMenu() {
                CompactWidthMode = true
            };
            items.Clear();

            menu.Add(new TextMenu.Header(Dialog.Clean("updater_versions_title")));

            menu.Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("updater_versions_current").Replace("((version))", Everest.BuildString)));

            var currentBranch = new TextMenu.Option<Everest.Updater.Source>(Dialog.Clean("UPDATER_CURRENT_BRANCH"));
            menu.Add(currentBranch);

            currentBranchName = new TextMenuExt.SubHeaderExt("") {
                HeightExtra = 0f
            };
            menu.Add(currentBranchName);

            Everest.Updater.Source currentSource = null;
            foreach (Everest.Updater.Source source in Everest.Updater.Sources) {
                currentBranch.Add(source.Name.DialogCleanOrNull() ?? source.Name, source, source.Name == CoreModule.Settings.CurrentBranch);
                if (source.Name == CoreModule.Settings.CurrentBranch)
                    currentSource = source;
            }
            currentBranch.Change(ReloadItems);

            ReloadItems(currentSource ?? currentBranch.Values[0].Item2);

            return menu;
        }

        private void ReloadItems(Everest.Updater.Source source) {
            // Abuse using statements to avoid having to refactor again
            using var scopeFinalizer = new ScopeFinalizer(() => {
                // Do this afterwards as the menu has now properly updated its size.
                for (int i = 0; i < items.Count; i++)
                    Add(new Coroutine(FadeIn(i, items[i])));

                if (menu.Height > menu.ScrollableMinSize) {
                    menu.Position.Y = menu.ScrollTargetY;
                }
            });
            using var batchModeContext = new TextMenuExt.BatchModeContext((patch_TextMenu) menu);

            foreach (TextMenu.Item item in items)
                menu.Remove(item);
            items.Clear();

            currentBranchName.Title = source.Description.DialogCleanOrNull() ?? source.Description;

            if (source.ErrorDialog != null) {
                string text = source.ErrorDialog.DialogClean();
                TextMenuExt.SubHeaderExt error = new TextMenuExt.SubHeaderExt(text) {
                    Alpha = 0f,
                    AlwaysCenter = true,
                };
                menu.Add(error);
                items.Add(error);
                return;
            }

            if (source.Entries == null) {
                TextMenuExt.SubHeaderExt info = new TextMenuExt.SubHeaderExt(Dialog.Clean("updater_versions_requesting")) {
                    Alpha = 0f,
                    AlwaysCenter = true,
                };
                menu.Add(info);
                items.Add(info);
                return;
            }

            int count = 0;
            foreach (Everest.Updater.Entry entry in source.Entries) {
                if (count >= buildsPerBranch)
                    continue;
                count++;
                TextMenuExt.ButtonExt item = new TextMenuExt.ButtonExt(entry.Name) {
                    Alpha = 0f,
                    AlwaysCenter = true,
                };
                menu.Add(item.Pressed(() => {
                    Everest.Updater.Update(OuiModOptions.Instance.Overworld.Goto<OuiLoggedProgress>(), entry);
                }));
                items.Add(item);
                if (!string.IsNullOrWhiteSpace(entry.Description)) {
                    string description = entry.Description;
                    // Recommended commit title max length
                    if (description.Length > 50)
                        description = description.Substring(0, 50) + "...";
                    var info = new TextMenuExt.SubHeaderExt(description) {
                        Alpha = 0f,
                        HeightExtra = 0f,
                        AlwaysCenter = true,
                    };
                    menu.Add(info);
                    items.Add(info);
                }
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
            Audio.Play(SFX.ui_main_whoosh_large_out);
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
                Audio.Play(SFX.ui_main_button_back);
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
