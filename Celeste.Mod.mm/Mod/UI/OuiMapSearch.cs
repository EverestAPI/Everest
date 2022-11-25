using FMOD.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.UI {
    public class OuiMapSearch : Oui {

        private MountainCamera cameraStart;
        private MountainCamera cameraEnd;

        public List<OuiChapterSelectIcon> OuiIcons;

        private SearchMenu menu;

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        private float alpha = 0f;

        private Color searchBarColor;

        private List<TextMenuExt.IItemExt> items = new List<TextMenuExt.IItemExt>();

        private List<string> sets = new List<string>();

        public bool FromChapterSelect = false;

        public bool Searching;

        private string search = "";
        private string searchPrev = "";

        private TextMenu.Item searchTitle;

        private bool searchConsumedButton;

        private int itemCount;
        private int matchCount;

        private bool quickMatched = false;

        private TextMenu.SubHeader resultHeader;

        private static TextMenuExt.SubHeaderExt perfectMatchHeader;

        private class SearchMenu : Entity {

            public bool leftFocused {
                get => leftMenu.Focused;
                set {
                    leftMenu.Focused = value;
                    rightMenu.Focused = !value;
                }
            }

            public TextMenu leftMenu;
            public TextMenu rightMenu;
            private float leftOffset;
            private float rightOffset;

            public int Selection {
                get => currentMenu.Selection;
                set => currentMenu.Selection = value;
            }

            public TextMenu currentMenu {
                get {
                    return leftFocused ? leftMenu : rightMenu;
                }
            }

            public bool Focused {
                get => leftMenu.Focused || rightMenu.Focused;
                set {
                    if (value) {
                        leftFocused = true;
                    } else {
                        leftMenu.Focused = false;
                        rightMenu.Focused = false;
                    }
                }
            }

            public SearchMenu(TextMenu leftMenu, TextMenu rightMenu) {
                Position = Vector2.Zero;
                this.leftMenu = leftMenu;
                this.rightMenu = rightMenu;
            }

            public override void Added(Scene scene) {
                base.Added(scene);
                rightMenu.InnerContent = TextMenu.InnerContentMode.TwoColumn;
                leftMenu.Position.X = Engine.Width / -4f;
                rightMenu.Position.X = Engine.Width / 4f;
                leftOffset = leftMenu.Position.X;
                rightOffset = rightMenu.Position.X;
                rightMenu.Focused = false;
                scene.Add(leftMenu);
                scene.Add(rightMenu);
            }

            public override void Removed(Scene scene) {
                scene.Remove(leftMenu);
                scene.Remove(rightMenu);
                base.Removed(scene);
            }

            public override void Update() {
                base.Update();
                leftMenu.Position.X = leftOffset + Position.X;
                rightMenu.Position.X = rightOffset + Position.X;
            }

        }

        private void clearSearch() {
            search = "";
        }

        private void cleanExit() {
            clearSearch();
            if (FromChapterSelect) {
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiChapterSelect>();
            } else {
                Overworld.Goto<OuiMapList>();
            }
        }

        public void OnTextInput(char c) {

            if (!Searching)
                return;

            if (c == (char) 13) {
                // Enter
                Scene.OnEndOfFrame += () => {
                    switchMenu();

                    if (items.Count >= 1) {
                        if (items.Count == 2 || matchCount == 1) {
                            Action pressed = (items[1] as TextMenuExt.ButtonExt)?.OnPressed;
                            if (pressed != null) {
                                clearSearch();
                                pressed.Invoke();
                                return;
                            }
                        }

                        int index = menu.rightMenu.GetItems().FindIndex(item => item is TextMenuExt.ButtonExt button && button.Selectable && items.Contains(button));
                        if (index > 0) {
                            menu.rightMenu.Selection = index;
                            Audio.Play(SFX.ui_main_button_select);
                        }
                    }
                };

                goto ValidButton;

            } else if (c == (char) 8) {
                // Backspace - trim.
                if (search.Length > 0) {
                    search = search.Substring(0, search.Length - 1);
                    Audio.Play(SFX.ui_main_rename_entry_backspace);
                    goto ValidButton;
                } else {
                    if (Input.MenuCancel.Pressed) {
                        Audio.Play(SFX.ui_main_button_back);
                        switchMenu();
                        goto ValidButton;
                    }
                    return;
                }

            } else if (c == ' ') {
                // Space - append.
                if (search.Length > 0) {
                    if (ActiveFont.Measure(search + c + "_").X < 542)
                        search += c;
                }
                Audio.Play(SFX.ui_main_rename_entry_space);
                goto ValidButton;

            } else if (!char.IsControl(c)) {
                // Any other character - append.
                if (ActiveFont.FontSize.Characters.ContainsKey(c)) {
                    Audio.Play(SFX.ui_main_rename_entry_char);
                    if (ActiveFont.Measure(search + c + "_").X < 542)
                        search += c;
                    goto ValidButton;
                } else {
                    goto InvalidButton;
                }
            }

            return;

            ValidButton:
            searchConsumedButton = true;
            MInput.Disabled = true;
            MInput.UpdateNull();
            return;

            InvalidButton:
            Audio.Play(SFX.ui_main_button_invalid);
            return;
        }

        private SearchMenu CreateMenu(bool inGame, EventInstance snapshot) {
            menu = new SearchMenu(new TextMenu(), new TextMenu());
            items.Clear();

            menu.leftMenu.Add(searchTitle = new TextMenu.Header(Dialog.Clean("maplist_search")));

            menu.rightMenu.Add(resultHeader = new patch_TextMenu.patch_SubHeader(string.Format(itemCount == 1 ? Dialog.Get("maplist_results_singular") : Dialog.Get("maplist_results_plural"), itemCount)));

            ReloadItems();

            return menu;
        }

        private void ReloadItems() {
            itemCount = 0;
            matchCount = 0;

            ((patch_TextMenu) menu.rightMenu).BatchMode = true;

            foreach (TextMenu.Item item in items)
                menu.rightMenu.Remove(item);
            items.Clear();

            string lastLevelSet = null;
            LevelSetStats levelSetStats = null;
            int levelSetAreaOffset = 0;
            int levelSetUnlockedAreas = int.MaxValue;
            int levelSetUnlockedModes = int.MaxValue;
            string name;

            string[] searchHunks = search.Split(' ').Select(hunk => hunk.ToLower()).ToArray();
            bool matched = false;

            for (int i = 0; i < AreaData.Areas.Count; i++) {
                AreaData area = AreaData.Get(i);
                string id = area.Name;
                name = id.DialogCleanOrNull() ?? id.SpacedPascalCase();
                if (name.ToLower() == search.ToLower()) {
                    matchCount++;
                }
            }

            SaveData save = SaveData.Instance;
            List<AreaStats> areaStatsAll = save.Areas;
            for (int i = 0; i < AreaData.Areas.Count; i++) {
                AreaData area = AreaData.Get(i);
                if (area == null || !area.HasMode(AreaMode.Normal))
                    continue;

                // TODO: Make subchapters hidden by default in the map list, even in debug mode.
                if (!save.DebugMode && !string.IsNullOrEmpty(area.GetMeta()?.Parent))
                    continue;

                string levelSet = area.GetLevelSet();

                string id = area.Name;
                name = id.DialogCleanOrNull() ?? id.SpacedPascalCase();

                if (levelSet == "Celeste" && i > levelSetAreaOffset + levelSetUnlockedAreas)
                    continue;

                List<string> matchTargets = new List<string> {
                    id,
                    name,
                    levelSet,
                    DialogExt.CleanLevelSet(levelSet)
                }.Select(text => text.ToLower()).ToList();

                List<string> unmatchedHunks = searchHunks.ToList();

                foreach (string hunk in searchHunks) {
                    if (matchTargets.Any(target => target.Contains(hunk)))
                        unmatchedHunks.Remove(hunk);
                }

                if (unmatchedHunks.Count > 0)
                    continue;

                itemCount++;

                TextMenuExt.ButtonExt button = new TextMenuExt.ButtonExt(name);
                button.Alpha = 0f;

                if (area.Icon != "areas/null")
                    button.Icon = area.Icon;
                button.IconWidth = 64f;

                if (levelSet == "Celeste" && i > levelSetAreaOffset + levelSetUnlockedAreas)
                    button.Disabled = true;

                if (name.ToLower() == search.ToLower()) {
                    if (!matched) {
                        perfectMatchHeader = new TextMenuExt.SubHeaderExt(Dialog.Clean("maplist_search_match"));
                        menu.rightMenu.Insert(0, perfectMatchHeader);
                        items.Insert(0, perfectMatchHeader);
                        matched = true;
                    }

                    menu.rightMenu.Insert(1, button.Pressed(() => {
                        clearSearch();
                        Inspect(area, AreaMode.Normal);
                    }));
                    items.Insert(1, button);

                    if (matchCount > 1) {
                        lastLevelSet = levelSet;
                        levelSetStats = SaveData.Instance.GetLevelSetStatsFor(levelSet);
                        levelSetAreaOffset = levelSetStats.AreaOffset;
                        levelSetUnlockedAreas = levelSetStats.UnlockedAreas;
                        levelSetUnlockedModes = levelSetStats.UnlockedModes;
                        string setname = DialogExt.CleanLevelSet(levelSet);
                        TextMenuExt.SubHeaderExt levelSetHeader = new TextMenuExt.SubHeaderExt(setname);
                        levelSetHeader.Alpha = 0f;
                        menu.rightMenu.Insert(1, levelSetHeader);
                        items.Insert(1, levelSetHeader);
                    }
                } else {

                    if (lastLevelSet != levelSet) {
                        lastLevelSet = levelSet;
                        levelSetStats = SaveData.Instance.GetLevelSetStatsFor(levelSet);
                        levelSetAreaOffset = levelSetStats.AreaOffset;
                        levelSetUnlockedAreas = levelSetStats.UnlockedAreas;
                        levelSetUnlockedModes = levelSetStats.UnlockedModes;
                        string setname = DialogExt.CleanLevelSet(levelSet);
                        TextMenuExt.SubHeaderExt levelSetHeader = new TextMenuExt.SubHeaderExt(setname);
                        levelSetHeader.Alpha = 0f;
                        menu.rightMenu.Add(levelSetHeader);
                        items.Add(levelSetHeader);
                    }

                    menu.rightMenu.Add(button.Pressed(() => {
                        clearSearch();
                        Inspect(area, AreaMode.Normal);
                    }));

                    items.Add(button);
                }
            }

            if (resultHeader != null) {
                resultHeader.Title = string.Format(itemCount == 1 ? Dialog.Get("maplist_results_singular") : Dialog.Get("maplist_results_plural"), itemCount);
            }

            ((patch_TextMenu) menu.rightMenu).BatchMode = false;

            // compute a delay so that options don't take more than a second to show up if many mods are installed.
            float delayBetweenOptions = 0.03f;
            if (items.Count > 0)
                delayBetweenOptions = Math.Min(0.03f, 1f / items.Count);

            // Do this afterwards as the menu has now properly updated its size.
            for (int i = 0; i < items.Count; i++)
                Add(new Coroutine(FadeIn(i, delayBetweenOptions, items[i])));

            menu.rightMenu.Selection = itemCount > 0 ? 1 : 0;

            if (menu.rightMenu.Height > menu.rightMenu.ScrollableMinSize) {
                menu.rightMenu.Position.Y = menu.rightMenu.ScrollTargetY;
            }

            quickMatched = matched;

            // Don't allow pressing any buttons while searching
            foreach (TextMenu.Item item in items)
                item.Disabled = Searching;
        }

        private IEnumerator FadeIn(int i, float delayBetweenOptions, TextMenuExt.IItemExt item) {
            yield return delayBetweenOptions * i;
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
            bool leftSelected = true;
            if (menu != null) {
                position = menu.Position;
                leftSelected = menu.leftFocused;
                selected = menu.Selection;
                Scene.Remove(menu);
            }

            menu = CreateMenu(false, null);

            if (selected >= 0) {
                menu.Selection = selected;
                menu.Position = position;
            }
            IEnumerable<TextMenu> menus = Scene.Entities.OfType<TextMenu>();
            Scene.Remove(menus);
            Scene.Add(menu);
        }

        public override IEnumerator Enter(Oui from) {
            TextInput.OnInput += OnTextInput;

            searchBarColor = Color.DarkSlateGray;
            searchBarColor.A = 80;

            FromChapterSelect = !(from is OuiMapList);

            Searching = true;

            ReloadMenu();

            menu.rightMenu.MinWidth = menu.rightMenu.Width;

            menu.Visible = (Visible = true);
            menu.Focused = false;

            cameraStart = Overworld.Mountain.UntiltedCamera;
            cameraEnd = cameraStart;
            cameraEnd.Position = cameraEnd.Position + -cameraStart.Rotation.Forward() * 1f;
            Overworld.Mountain.EaseCamera(Overworld.Mountain.Area, cameraEnd, 2f);
            Overworld.Mountain.AllowUserRotation = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = offScreenX + -1920f * Ease.CubeOut(p);
                alpha = Ease.CubeOut(p);
                yield return null;
            }

            menu.Focused = true;
        }

        public override IEnumerator Leave(Oui next) {
            TextInput.OnInput -= OnTextInput;

            FromChapterSelect = false;

            MInput.Disabled = false;

            menu.Focused = false;

            Audio.Play(SFX.ui_main_whoosh_large_out);

            if (Overworld != null) {
                Overworld.Maddy.Show = true;
                Overworld.Mountain.EaseCamera(Overworld.Mountain.Area, cameraStart, 0.4f);
            }

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = onScreenX + 1920f * Ease.CubeIn(p);
                alpha = 1f - Ease.CubeIn(p);
                yield return null;
            }

            menu.Visible = Visible = false;
            Searching = false;
            menu.RemoveSelf();
            menu = null;
        }

        private bool switchMenu() {
            bool nextIsLeft = !menu.leftFocused;
            if (nextIsLeft || items.Count > 1) {
                menu.leftFocused = nextIsLeft;
                Searching = nextIsLeft;
                MInput.Disabled = nextIsLeft;
                int resultIndex = quickMatched ? 1 : 2;
                menu.currentMenu.Selection = nextIsLeft ? -1 : resultIndex;
                if (nextIsLeft) {
                    Audio.Play(SFX.ui_main_button_toggle_off);
                } else {
                    Audio.Play(SFX.ui_main_button_toggle_on);
                }
                return true;
            }
            return false;
        }

        public override void Update() {
            if (Searching) {
                if (MInput.Keyboard.Pressed(Keys.Delete)) {
                    if (search.Length > 0) {
                        clearSearch();
                        Audio.Play(SFX.ui_main_rename_entry_backspace);
                    } else {
                        Audio.Play(SFX.ui_main_button_back);
                        cleanExit();
                    }
                    searchConsumedButton = true;
                    MInput.UpdateNull();
                }
                MInput.Disabled = searchConsumedButton;
            }
            searchConsumedButton = false;

            if (menu != null && menu.Focused && Selected) {
                Overworld.Maddy.Show = false;

                if (search != searchPrev) {
                    ReloadItems();
                    searchPrev = search;
                }

                if (Input.MenuCancel.Pressed || Input.Pause.Pressed || Input.ESC.Pressed) {
                    if (Searching && search != "") {
                        if (!switchMenu()) {
                            cleanExit();
                        }
                    } else {
                        cleanExit();
                    }
                }

                if (Input.MenuRight.Pressed) {
                    if (!menu.leftFocused)
                        return;
                    switchMenu();
                }

                if (Input.MenuLeft.Pressed) {
                    if (menu.leftFocused)
                        return;
                    switchMenu();
                }

            }

            base.Update();

            if (Searching) {
                // Otherwise spacebar will turn on free cam while searching
                ((patch_MountainRenderer) Overworld.Mountain).SetFreeCam(false);
            }

            // Don't allow pressing any buttons while searching
            if (menu != null)
                foreach (TextMenu.Item item in menu.rightMenu.GetItems())
                    item.Disabled = Searching;
        }

        public override void Render() {
            if (alpha > 0f)
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * alpha * 0.4f);

            TextMenu leftMenu = menu.leftMenu;
            // Draw the search
            if (searchTitle != null) {
                Vector2 value = leftMenu.Position + leftMenu.Justify * new Vector2(leftMenu.Width, leftMenu.Height);
                Vector2 pos = new Vector2(value.X - 200f, value.Y + leftMenu.GetYOffsetOf(searchTitle) + 1f);
                Draw.Rect(pos + new Vector2(-8f, 32f), 416, (int) (ActiveFont.HeightOf("l" + search) + 8) * -1, searchBarColor);
                ActiveFont.DrawOutline(search + (Searching ? "_" : ""), pos, new Vector2(0f, 0.5f), Vector2.One * 0.75f, Color.White * leftMenu.Alpha, 2f, Color.Black * (leftMenu.Alpha * leftMenu.Alpha * leftMenu.Alpha));
            }

            base.Render();
        }

        public override void SceneEnd(Scene scene) {
            TextInput.OnInput -= OnTextInput;
            MInput.Disabled = false;
        }

        protected void Inspect(AreaData area, AreaMode mode = AreaMode.Normal) {
            Focused = false;
            Audio.Play(SFX.ui_world_icon_select);
            SaveData.Instance.LastArea = area.ToKey(mode);
            if (OuiIcons != null && area.ID < OuiIcons.Count)
                OuiIcons[area.ID].Select();
            Overworld.Mountain.Model.EaseState(area.MountainState);
            Overworld.Goto<OuiChapterPanel>();
        }

        private void Start(AreaData area, AreaMode mode = AreaMode.Normal, string checkpoint = null) {
            Focused = false;
            Audio.Play(SFX.ui_world_chapter_checkpoint_start);
            Add(new Coroutine(StartRoutine(area, mode, checkpoint)));
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
            LevelEnter.Go(new Session(area.ToKey(mode), checkpoint), false);
        }

    }
}