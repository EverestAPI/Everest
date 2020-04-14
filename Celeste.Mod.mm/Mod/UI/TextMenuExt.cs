using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    public static partial class TextMenuExt {

        public static void DrawIcon(Vector2 position, string iconName, float? inputWidth, float inputHeight, bool outline, Color color, ref Vector2 textPosition) {
            if (iconName == null || !GFX.Gui.Has(iconName))
                return;

            MTexture icon = GFX.Gui[iconName];

            float width = inputWidth ?? icon.Width;
            float height = inputHeight;

            float scale = height / icon.Height;
            if (width > icon.Width)
                scale = MathHelper.Min(scale, width / icon.Width);

            if (outline)
                icon.DrawOutlineCentered(new Vector2(position.X + width * 0.5f * scale, position.Y), color, scale);
            else
                icon.DrawCentered(new Vector2(position.X + width * 0.5f * scale, position.Y), color, scale);

            textPosition.X += inputWidth ?? (icon.Width * scale);
        }

        public interface IItemExt {

            Color TextColor { get; set; }

            string Icon { get; set; }
            float? IconWidth { get; set; }
            bool IconOutline { get; set; }

            Vector2 Offset { get; set; }
            float Alpha { get; set; }

        }

        public class ButtonExt : TextMenu.Button, IItemExt {

            public Color TextColor { get; set; } = Color.White;
            public Color TextColorDisabled { get; set; } = Color.DarkSlateGray;

            public string Icon { get; set; }
            public float? IconWidth { get; set; }
            public bool IconOutline { get; set; }

            public Vector2 Offset { get; set; }
            public float Alpha { get; set; } = 1f;

            public ButtonExt(string label, string icon = null)
                : base(label) {
                Icon = icon;
            }

            public override void Render(Vector2 position, bool highlighted) {
                position += Offset;
                float alpha = Container.Alpha * Alpha;

                Color textColor = (Disabled ? TextColorDisabled : highlighted ? Container.HighlightColor : TextColor) * alpha;
                Color strokeColor = Color.Black * (alpha * alpha * alpha);

                bool flag = Container.InnerContent == TextMenu.InnerContentMode.TwoColumn && !AlwaysCenter;

                Vector2 textPosition = position + (flag ? Vector2.Zero : new Vector2(Container.Width * 0.5f, 0f));
                Vector2 justify = flag ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);

                DrawIcon(
                    position,
                    Icon,
                    IconWidth,
                    Height(),
                    IconOutline,
                    (Disabled ? Color.DarkSlateGray : highlighted ? Color.White : Color.LightSlateGray) * alpha,
                    ref textPosition
                );

                ActiveFont.DrawOutline(Label, textPosition, justify, Vector2.One, textColor, 2f, strokeColor);
            }

        }

        public class SubHeaderExt : TextMenu.SubHeader, IItemExt {

            public Color TextColor { get; set; } = Color.Gray;

            public string Icon { get; set; }
            public float? IconWidth { get; set; }
            public bool IconOutline { get; set; }

            public Vector2 Offset { get; set; }
            public float Alpha { get; set; } = 1f;

            public float HeightExtra { get; set; } = 48f;

            public override float Height() => base.Height() - 48f + HeightExtra;

            public SubHeaderExt(string title, string icon = null)
                : base(title) {
                Icon = icon;
            }

            public override void Render(Vector2 position, bool highlighted) {
                position += Offset;
                float alpha = Container.Alpha * Alpha;

                Color strokeColor = Color.Black * (alpha * alpha * alpha);

                Vector2 textPosition = position + (
                    Container.InnerContent == TextMenu.InnerContentMode.TwoColumn ?
                    new Vector2(0f, MathHelper.Max(0f, 32f - 48f + HeightExtra)) :
                    new Vector2(Container.Width * 0.5f, MathHelper.Max(0f, 32f - 48f + HeightExtra))
                );
                Vector2 justify = new Vector2(Container.InnerContent == TextMenu.InnerContentMode.TwoColumn ? 0f : 0.5f, 0.5f);

                DrawIcon(
                    position,
                    Icon,
                    IconWidth,
                    Height(),
                    IconOutline,
                    Color.White * alpha,
                    ref textPosition
                );

                if (Title.Length <= 0)
                    return;

                ActiveFont.DrawOutline(Title, textPosition, justify, Vector2.One * 0.6f, TextColor * alpha, 2f, strokeColor);
            }

        }

        public class HeaderImage : TextMenu.Item {

            public string Image { get; set; }
            public bool ImageOutline { get; set; }
            public Color ImageColor { get; set; } = Color.White;
            public float ImageScale { get; set; } = 1f;

            public Vector2 Offset { get; set; }
            public float Alpha { get; set; } = 1f;

            public HeaderImage(string image = null) {
                Image = image;
                Selectable = false;
                IncludeWidthInMeasurement = false;
            }

            public override float LeftWidth() {
                if (Image == null || !GFX.Gui.Has(Image))
                    return 0f;
                MTexture image = GFX.Gui[Image];
                return image.Width * ImageScale;
            }

            public override float Height() {
                if (Image == null || !GFX.Gui.Has(Image))
                    return 0f;
                MTexture image = GFX.Gui[Image];
                return image.Height * ImageScale;
            }

            public override void Render(Vector2 position, bool highlighted) {
                position += Offset;
                float alpha = Container.Alpha * Alpha;

                Color strokeColor = Color.Black * (alpha * alpha * alpha);

                if (Image == null || !GFX.Gui.Has(Image))
                    return;
                MTexture image = GFX.Gui[Image];

                if (ImageOutline)
                    image.DrawOutlineJustified(position, Vector2.UnitY * 0.5f, ImageColor, ImageScale);
                else
                    image.DrawJustified(position, Vector2.UnitY * 0.5f, ImageColor, ImageScale);
            }

        }

        /// <summary>
        /// Sub-header that eases in/out when FadeVisible is changed.
        /// </summary>
        public class EaseInSubHeaderExt : SubHeaderExt {

            /// <summary>
            /// Toggling this will make the header ease in/out.
            /// </summary>
            public bool FadeVisible { get; set; } = true;

            private float uneasedAlpha;
            private TextMenu containingMenu;

            /// <summary>
            /// Creates a EaseInSubHeaderExt.
            /// </summary>
            /// <param name="title">The sub-header title</param>
            /// <param name="initiallyVisible">The initial value for FadeVisible</param>
            /// <param name="containingMenu">The menu containing this SubHeader</param>
            /// <param name="icon">An icon for the sub-header</param>
            public EaseInSubHeaderExt(string title, bool initiallyVisible, TextMenu containingMenu, string icon = null)
                : base(title, icon) {

                this.containingMenu = containingMenu;

                FadeVisible = initiallyVisible;
                Alpha = FadeVisible ? 1 : 0;
                uneasedAlpha = Alpha;
            }

            // the fade has to take into account the item spacing as well, or the other options will abruptly shift up when Visible is switched to false.
            public override float Height() => MathHelper.Lerp(-containingMenu.ItemSpacing, base.Height(), Alpha);

            public override void Update() {
                base.Update();

                // gradually make the sub-header fade in or out. (~333ms fade)
                float targetAlpha = FadeVisible ? 1 : 0;
                if (uneasedAlpha != targetAlpha) {
                    uneasedAlpha = Calc.Approach(uneasedAlpha, targetAlpha, Engine.DeltaTime * 3f);

                    if (FadeVisible)
                        Alpha = Ease.SineOut(uneasedAlpha);
                    else
                        Alpha = Ease.SineIn(uneasedAlpha);
                }

                Visible = (Alpha != 0);
            }
        }

        /// <summary>
		/// A Slider optimized for large integer ranges.<br></br>
		/// Inherits directly from <see cref="TextMenu.Item"/>
		/// </summary>
        public class IntSlider : TextMenu.Item {
            public string Label;
            public int Index;
            public Action<int> OnValueChange;
            public int PreviousIndex;
            private float sine;
            private int lastDir;
            private int min;
            private int max;
            private float fastMoveTimer;

            /// <summary>
            /// Creates a new <see cref="IntSlider"/>
            /// </summary>
            /// <param name="label">Slider label</param>
            /// <param name="min">Minimum possible value</param>
            /// <param name="max">Maximum possible value</param>
            /// <param name="value">Initial value<br></br>Restricted between min and max</param>
            public IntSlider(string label, int min, int max, int value = 0) {
                Label = label;
                Selectable = true;
                this.min = min;
                this.max = max;
                Index = (value < min) ? min : (value > max) ? max : value;
            }
            public IntSlider Change(Action<int> action) {
                OnValueChange = action;
                return this;
            }
            public override void Added() {
                Container.InnerContent = TextMenu.InnerContentMode.TwoColumn;
            }
            public override void LeftPressed() {
                if (Input.MenuLeft.Repeating)
                    fastMoveTimer += Engine.DeltaTime * 8;
                else
                    fastMoveTimer = 0;

                if (Index > min) {
                    Audio.Play("event:/ui/main/button_toggle_off");
                    PreviousIndex = Index;
                    Index -= (fastMoveTimer < 1) ? 1 : (fastMoveTimer < 3) ? 10 : 25;
                    Index = Math.Max(min, Index); // ensure we stay within bounds
                    lastDir = -1;
                    ValueWiggler.Start();
                    OnValueChange?.Invoke(Index);
                }
            }
            public override void RightPressed() {
                if (Input.MenuRight.Repeating)
                    fastMoveTimer += Engine.DeltaTime * 8;
                else
                    fastMoveTimer = 0;

                if (Index < max) {
                    Audio.Play("event:/ui/main/button_toggle_on");
                    PreviousIndex = Index;
                    Index += (fastMoveTimer < 1) ? 1 : (fastMoveTimer < 3) ? 10 : 25;
                    Index = Math.Min(max, Index); // ensure we stay within bounds
                    lastDir = 1;
                    ValueWiggler.Start();
                    OnValueChange?.Invoke(Index);
                }
            }
            public override void ConfirmPressed() {
                if ((max - min) == 1) {
                    if (Index == min) {
                        Audio.Play("event:/ui/main/button_toggle_on");
                    } else {
                        Audio.Play("event:/ui/main/button_toggle_off");
                    }
                    PreviousIndex = Index;
                    lastDir = ((Index == min) ? 1 : -1);
                    Index = ((Index == min) ? max : min);
                    ValueWiggler.Start();
                    OnValueChange?.Invoke(Index);
                }
            }

            public override void Update() {
                sine += Engine.RawDeltaTime;
            }
            public override float LeftWidth() {
                return ActiveFont.Measure(Label).X + 32f;
            }
            public override float RightWidth() {
                //Measure Index in case it is externally set ouside the bounds
                float width = Calc.Max(0f, ActiveFont.Measure(max.ToString()).X, ActiveFont.Measure(min.ToString()).X, ActiveFont.Measure(Index.ToString()).X);
                return width + 120f;
            }
            public override float Height() {
                return ActiveFont.LineHeight;
            }

            public override void Render(Vector2 position, bool highlighted) {
                float alpha = Container.Alpha;
                Color strokeColor = Color.Black * (alpha * alpha * alpha);
                Color color = Disabled ? Color.DarkSlateGray : ((highlighted ? Container.HighlightColor : Color.White) * alpha);
                ActiveFont.DrawOutline(Label, position, new Vector2(0f, 0.5f), Vector2.One, color, 2f, strokeColor);
                if ((max - min) > 0) {
                    float rWidth = RightWidth();
                    ActiveFont.DrawOutline(Index.ToString(), position + new Vector2(Container.Width - rWidth * 0.5f + lastDir * ValueWiggler.Value * 8f, 0f), new Vector2(0.5f, 0.5f), Vector2.One * 0.8f, color, 2f, strokeColor);
                    
                    Vector2 vector = Vector2.UnitX * (float)(highlighted ? (Math.Sin(sine * 4f) * 4f) : 0f);

                    Vector2 position2 = position + new Vector2(Container.Width - rWidth + 40f + ((lastDir < 0) ? (-ValueWiggler.Value * 8f) : 0f), 0f) - (Index > min ? vector : Vector2.Zero);
                    ActiveFont.DrawOutline("<", position2, new Vector2(0.5f, 0.5f), Vector2.One, Index > min ? color : (Color.DarkSlateGray * alpha), 2f, strokeColor);

                    position2 = position + new Vector2(Container.Width - 40f + ((lastDir > 0) ? (ValueWiggler.Value * 8f) : 0f), 0f) + (Index < max ? vector : Vector2.Zero);
                    ActiveFont.DrawOutline(">", position2, new Vector2(0.5f, 0.5f), Vector2.One, Index < max ? color : (Color.DarkSlateGray * alpha), 2f, strokeColor);
                }
            }
        }

        /// <summary>
        /// <see cref="TextMenu.Item"/> that acts as a Submenu for other Items.
        /// <br></br><br></br>
        /// Currently does not support recursive submenus
        /// </summary>
        public class SubMenu : TextMenu.Item {
            public string Label;
            MTexture Icon;

            public List<TextMenu.Item> Items { get; private set; }

            private List<TextMenu.Item> delayedAddItems;

            public int Selection;

            public TextMenu.Item Current {
                get {
                    if (Items.Count <= 0 || Selection < 0) {
                        return null;
                    }
                    return Items[Selection];
                }
                set {
                    Selection = Items.IndexOf(value);
                }
            }

            public int FirstPossibleSelection {
                get {
                    for (int i = 0; i < Items.Count; i++) {
                        if (Items[i] != null && Items[i].Hoverable) {
                            return i;
                        }
                    }
                    return 0;
                }
            }

            public int LastPossibleSelection {
                get {
                    for (int i = Items.Count - 1; i >= 0; i--) {
                        if (Items[i] != null && Items[i].Hoverable) {
                            return i;
                        }
                    }
                    return 0;
                }
            }

            public float ScrollTargetY {
                get {
                    float min = Engine.Height - 150f - Container.Height * Container.Justify.Y;
                    float max = 150f + Container.Height * Container.Justify.Y;
                    return Calc.Clamp((float) (Engine.Height / 2) + Container.Height * Container.Justify.Y - GetYOffsetOf(Current), min, max);
                }
            }

            public float ItemSpacing;
            public float ItemIndent;
            private Color HighlightColor;
            public string ConfirmSfx;
            public bool AlwaysCenter;

            public float LeftColumnWidth;
            public float RightColumnWidth;

            public float TitleHeight { get; private set; }
            public float MenuHeight { get; private set; }

            public bool Focused;

            private bool enterOnSelect;
            private float ease;

            private bool containerAutoScroll;

            /// <summary>
            /// 
            /// </summary>
            /// <param name="label"></param>
            /// <param name="enterOnSelect">Expand submenu when selected</param>
            public SubMenu(string label, bool enterOnSelect) : base() {
                //Item Constructor
                ConfirmSfx = "event:/ui/main/button_select";
                Label = label;
                Icon = GFX.Gui["downarrow"];
                Selectable = true;
                IncludeWidthInMeasurement = true;

                this.enterOnSelect = enterOnSelect;

                OnEnter = delegate {
                    if (this.enterOnSelect) {
                        OnPressed();
                    }
                };
                OnPressed = delegate {
                    if (Items.Count > 0) {
                        Container.Focused = false;
                        Focused = true;
                        FirstSelection();
                    }
                };


                //Menu Constructor
                Items = new List<TextMenu.Item>();
                delayedAddItems = new List<TextMenu.Item>();
                Selection = -1;
                ItemSpacing = 4f;
                ItemIndent = 20f;
                HighlightColor = Color.White;

                RecalculateSize();
            }

            #region Menu

            /// <summary>
            /// Add any non-submenu <see cref="TextMenu.Item"/> to the Submenu
            /// </summary>
            /// <param name="item">Item to be added</param>
            /// <returns></returns>
            public SubMenu Add(TextMenu.Item item) {
                if (Container != null) {
                    Items.Add(item);
                    item.Container = Container;
                    Container.Add(item.ValueWiggler = Wiggler.Create(0.25f, 3f, null, false, false));
                    Container.Add(item.SelectWiggler = Wiggler.Create(0.25f, 3f, null, false, false));
                    item.ValueWiggler.UseRawDeltaTime = (item.SelectWiggler.UseRawDeltaTime = true);
                    if (Selection == -1) {
                        FirstSelection();
                    }
                    RecalculateSize();
                    item.Added();
                    return this;
                } else {
                    delayedAddItems.Add(item);
                    return this;
                }
            }

            /// <summary>
            /// Insert any non-submenu <see cref="TextMenu.Item"/> into the Submenu at <paramref name="index"/>
            /// </summary>
            /// <param name="index"></param>
            /// <param name="item">Item to be inserted</param>
            /// <returns></returns>v
            public SubMenu Insert(int index, TextMenu.Item item) {
                if (Container != null) {
                    Items.Insert(index, item);
                    item.Container = Container;
                    Container.Add(item.ValueWiggler = Wiggler.Create(0.25f, 3f, null, false, false));
                    Container.Add(item.SelectWiggler = Wiggler.Create(0.25f, 3f, null, false, false));
                    item.ValueWiggler.UseRawDeltaTime = (item.SelectWiggler.UseRawDeltaTime = true);
                    if (Selection == -1) {
                        FirstSelection();
                    }
                    RecalculateSize();
                    item.Added();
                    return this;
                } else {
                    delayedAddItems.Insert(index, item);
                    return this;
                }
            }

            /// <summary>
            /// Remove any non-submenu <see cref="TextMenu.Item"/> from the Submenu
            /// </summary>
            /// <param name="item">Item to be removed</param>
            /// <returns></returns>v
            public SubMenu Remove(TextMenu.Item item) {
                if (Container != null) {
                    if (!Items.Remove(item)) {
                        return this;
                    }
                    item.Container = null;
                    Container.Remove(item.ValueWiggler);
                    Container.Remove(item.SelectWiggler);
                    RecalculateSize();
                    return this;
                } else {
                    delayedAddItems.Remove(item);
                    return this;
                }
            }

            public void Clear() {
                Items = new List<TextMenu.Item>();
            }

            public int IndexOf(TextMenu.Item item) {
                return Items.IndexOf(item);
            }

            public void FirstSelection() {
                Selection = -1;
                MoveSelection(1, false);
            }

            public void MoveSelection(int direction, bool wiggle = false) {
                int selection = Selection;
                direction = Math.Sign(direction);
                int count = 0;
                foreach (TextMenu.Item item in Items) {
                    if (item.Hoverable)
                        count++;
                }
                do {
                    Selection += direction;
                    if (count > 2) {
                        if (Selection < 0) {
                            Selection = Items.Count - 1;
                        } else if (Selection >= Items.Count) {
                            Selection = 0;
                        }
                    } else if (Selection < 0 || Selection > Items.Count - 1) {
                        Selection = Calc.Clamp(Selection, 0, Items.Count - 1);
                        break;
                    }
                }
                while (!Current.Hoverable);

                if (!Current.Hoverable) {
                    Selection = selection;
                }
                if (Selection != selection && Current != null) {
                    if (selection >= 0 && Items[selection] != null && Items[selection].OnLeave != null) {
                        Items[selection].OnLeave();
                    }
                    Current.OnEnter?.Invoke();
                    if (wiggle) {
                        Audio.Play(direction > 0 ? "event:/ui/main/rollover_down" : "event:/ui/main/rollover_up");
                        Current.SelectWiggler.Start();
                    }
                }
            }

            public void RecalculateSize() {
                TitleHeight = ActiveFont.LineHeight;
                if (Items.Count < 1)
                    return;

                LeftColumnWidth = RightColumnWidth = MenuHeight = 0f;
                foreach (TextMenu.Item item in Items) {
                    if (item.IncludeWidthInMeasurement) {
                        LeftColumnWidth = Math.Max(LeftColumnWidth, item.LeftWidth());
                    }
                }
                foreach (TextMenu.Item item in Items) {
                    if (item.IncludeWidthInMeasurement) {
                        RightColumnWidth = Math.Max(RightColumnWidth, item.RightWidth());
                    }
                }
                foreach (TextMenu.Item item in Items) {
                    if (item.Visible) {
                        MenuHeight += item.Height() + Container.ItemSpacing;
                    }
                }
                MenuHeight -= Container.ItemSpacing;
            }

            public float GetYOffsetOf(TextMenu.Item item) {
                if (item == null) {
                    return 0f;
                }
                float offset = 0f;
                foreach (TextMenu.Item child in Items) {
                    if (child.Visible) {
                        offset += child.Height() + ItemSpacing;
                    }
                    if (child == item) {
                        break;
                    }
                }
                return offset - item.Height() * 0.5f - ItemSpacing + Container.GetYOffsetOf(this);
            }

            private void menu_Added() {
                foreach (TextMenu.Item item in delayedAddItems) {
                    Add(item);
                }
            }

            private void menu_Update() {
                OnUpdate?.Invoke();

                //ease check needed to eat the first input from Container
                if (Focused && ease > 0.9f) {
                    if (Input.MenuDown.Pressed) {
                        if (!Input.MenuDown.Repeating || Selection != LastPossibleSelection) {
                            MoveSelection(1, true);
                        }
                    } else if (Input.MenuUp.Pressed && (!Input.MenuUp.Repeating || Selection != FirstPossibleSelection)) {
                        MoveSelection(-1, true);
                    }
                    if (Current != null) {
                        if (Input.MenuLeft.Pressed) {
                            Current.LeftPressed();
                        }
                        if (Input.MenuRight.Pressed) {
                            Current.RightPressed();
                        }
                        if (Input.MenuConfirm.Pressed) {
                            Current.ConfirmPressed();
                            Current.OnPressed?.Invoke();
                        }
                        if (Input.MenuJournal.Pressed && Current.OnAltPressed != null) {
                            Current.OnAltPressed();
                        }
                    }
                    if (!Input.MenuConfirm.Pressed) {
                        if (Input.MenuCancel.Pressed || Input.ESC.Pressed || Input.Pause.Pressed) {
                            Focused = false;
                            Audio.Play("event:/ui/main/button_back");
                            Container.AutoScroll = containerAutoScroll;
                            Container.Focused = true;
                        }
                    }
                }

                foreach (TextMenu.Item item in Items) {
                    item.OnUpdate?.Invoke();
                    item.Update();
                }

                if (Settings.Instance.DisableFlashes) {
                    HighlightColor = TextMenu.HighlightColorA;
                } else if (Engine.Scene.OnRawInterval(0.1f)) {
                    if (HighlightColor == TextMenu.HighlightColorA) {
                        HighlightColor = TextMenu.HighlightColorB;
                    } else {
                        HighlightColor = TextMenu.HighlightColorA;
                    }
                }

                if (containerAutoScroll) {
                    if (Container.Height > Container.ScrollableMinSize) {
                        Container.Position.Y += (ScrollTargetY - Container.Position.Y) * (1f - (float) Math.Pow(0.01f, Engine.RawDeltaTime));
                        return;
                    }
                    Container.Position.Y = 540f;
                }
            }

            private void menu_Render(Vector2 position) {
                RecalculateSize();
                foreach (TextMenu.Item item in Items) {
                    if (item.Visible) {
                        float height = item.Height();
                        Vector2 vector = position + new Vector2(0f, height * 0.5f + item.SelectWiggler.Value * 8f);
                        if (vector.Y + height * 0.5f > 0f && vector.Y - height * 0.5f < Engine.Height) {
                            item.Render(vector, Focused && Current == item);
                        }
                        position.Y += height + ItemSpacing;
                    }
                }
            }

            #endregion

            #region TextMenu.Item

            public override void ConfirmPressed() {
                containerAutoScroll = Container.AutoScroll;
                Container.AutoScroll = false;
                Audio.Play(ConfirmSfx);
                base.ConfirmPressed();
            }

            public override float LeftWidth() {
                return ActiveFont.Measure(Label).X;
            }
            public override float RightWidth() {
                return Icon.Width;
            }

            public override float Height() {
                //If there are no items, MenuHeight will actually be a negative number
                if (Items.Count > 0)
                    return TitleHeight + (MenuHeight * Ease.QuadOut(ease));
                else
                    return TitleHeight;
            }

            public override void Added() {
                base.Added();
                menu_Added();
            }

            public override void Update() {
                if (Focused)
                    ease = Calc.Approach(ease, 1f, Engine.DeltaTime * 4f);
                else
                    ease = Calc.Approach(ease, 0f, Engine.DeltaTime * 4f);
                base.Update();
                menu_Update();
            }

            public override void Render(Vector2 position, bool highlighted) {
                Vector2 top = new Vector2(position.X, position.Y - (Height() / 2));

                float alpha = Container.Alpha;
                Color color = Disabled ? Color.DarkSlateGray : ((highlighted ? Container.HighlightColor : Color.White) * alpha);
                Color strokeColor = Color.Black * (alpha * alpha * alpha);

                bool uncentered = Container.InnerContent == TextMenu.InnerContentMode.TwoColumn && !AlwaysCenter;

                Vector2 titlePosition = top + (Vector2.UnitY * TitleHeight / 2) + (uncentered ? Vector2.Zero : new Vector2(Container.Width * 0.5f, 0f));
                Vector2 justify = uncentered ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
                Vector2 iconJustify = uncentered ? new Vector2(ActiveFont.Measure(Label).X + Icon.Width, 5f) : new Vector2(ActiveFont.Measure(Label).X / 2 + Icon.Width, 5f);
                SubMenu.DrawIcon(titlePosition, Icon, iconJustify, true, (Disabled || Items.Count < 1 ? Color.DarkSlateGray : (Focused ? Container.HighlightColor : Color.White)) * alpha, 0.8f);
                ActiveFont.DrawOutline(Label, titlePosition, justify, Vector2.One, color, 2f, strokeColor);

                if (Focused && ease > 0.9f) {
                    Vector2 menuPosition = new Vector2(top.X + ItemIndent, top.Y + TitleHeight + ItemSpacing);
                    menu_Render(menuPosition);
                }
            }

            private static void DrawIcon(Vector2 position, MTexture icon, Vector2 justify, bool outline, Color color, float scale) {
                if (outline) {
                    icon.DrawOutlineCentered(position + justify, color);
                } else {
                    icon.DrawCentered(position + justify, color, scale);
                }
            }

            #endregion
        }

        /// <summary>
		/// <see cref="TextMenu.Item"/> that acts as a Slider of Submenus for other Items.
		/// <br></br><br></br>
		/// Currently does not support recursive submenus
		/// </summary>
		public class OptionSubMenu : TextMenu.Item {
            public string Label;
            MTexture Icon;

            //Menus are stored as lists associated with a label
            public List<Tuple<string, List<TextMenu.Item>>> Menus { get; private set; }

            private List<Tuple<string, List<TextMenu.Item>>> delayedAddMenus;

            public int MenuIndex;

            private int InitialSelection;
            public int Selection;

            private int lastDir;
            private float sine;

            public Action<int> OnValueChange;

            public List<TextMenu.Item> CurrentMenu {
                get { return (Menus.Count > 0) ? Menus[MenuIndex].Item2 : null; }
            }

            public TextMenu.Item Current {
                get {
                    if (CurrentMenu.Count <= 0 || Selection < 0) {
                        return null;
                    }
                    return CurrentMenu[Selection];
                }
            }

            public int FirstPossibleSelection {
                get {
                    for (int i = 0; i < CurrentMenu.Count; i++) {
                        if (CurrentMenu[i] != null && CurrentMenu[i].Hoverable) {
                            return i;
                        }
                    }
                    return 0;
                }
            }

            public int LastPossibleSelection {
                get {
                    for (int i = CurrentMenu.Count - 1; i >= 0; i--) {
                        if (CurrentMenu[i] != null && CurrentMenu[i].Hoverable) {
                            return i;
                        }
                    }
                    return 0;
                }
            }

            public float ScrollTargetY {
                get {
                    float min = Engine.Height - 150 - Container.Height * Container.Justify.Y;
                    float max = 150f + Container.Height * Container.Justify.Y;
                    return Calc.Clamp(Engine.Height / 2 + Container.Height * Container.Justify.Y - GetYOffsetOf(Current), min, max);
                }
            }

            public float ItemSpacing;
            public float ItemIndent;

            private Color HighlightColor;

            public string ConfirmSfx;

            public bool AlwaysCenter;

            public float LeftColumnWidth;
            public float RightColumnWidth;

            public float TitleHeight { get; private set; }

            //Accessor property used to smootly(ish) transition between menu heights
            public float MenuHeight { get; private set; }
            private float _MenuHeight;

            public bool Focused;
            private bool wasFocused;

            private bool containerAutoScroll;

            public OptionSubMenu(string label) : base() {
                //Item Constructor
                ConfirmSfx = "event:/ui/main/button_select";
                Label = label;
                Icon = GFX.Gui["downarrow"];

                Selectable = true;
                IncludeWidthInMeasurement = true;

                MenuIndex = 0;


                //Menu Constructor
                Menus = new List<Tuple<string, List<TextMenu.Item>>>();
                delayedAddMenus = new List<Tuple<string, List<TextMenu.Item>>>();

                Selection = -1;
                ItemSpacing = 4f;
                ItemIndent = 20f;
                HighlightColor = Color.White;

                RecalculateSize();
            }

            #region Menu

            /// <summary>
            /// Add a list of non-submenu <see cref="TextMenu.Item"/>s to the Submenu
            /// </summary>
            /// <param name="label">Displayed submenu label</param>
            /// <param name="items">Items to be added to the submenu</param>
            /// <returns></returns>
            public OptionSubMenu Add(string label, List<TextMenu.Item> items) {
                if (Container != null) {
                    if (items != null) {
                        foreach (TextMenu.Item item in items) {
                            item.Container = Container;
                            Container.Add(item.ValueWiggler = Wiggler.Create(0.25f, 3f, null, false, false));
                            Container.Add(item.SelectWiggler = Wiggler.Create(0.25f, 3f, null, false, false));
                            item.ValueWiggler.UseRawDeltaTime = (item.SelectWiggler.UseRawDeltaTime = true);
                            item.Added();
                        }
                        Menus.Add(new Tuple<string, List<TextMenu.Item>>(label, items));
                    } else
                        Menus.Add(new Tuple<string, List<TextMenu.Item>>(label, new List<TextMenu.Item>()));

                    if (Selection == -1) {
                        FirstSelection();
                    }
                    RecalculateSize();
                    return this;
                } else {
                    delayedAddMenus.Add(new Tuple<string, List<TextMenu.Item>>(label, items));
                    return this;
                }
            }

            public OptionSubMenu SetInitialSelection(int index) {
                InitialSelection = index;
                return this;
            }

            public void Clear() {
                Menus = new List<Tuple<string, List<TextMenu.Item>>>();
            }

            public void FirstSelection() {
                Selection = -1;
                if (CurrentMenu.Count > 0)
                    MoveSelection(1, true);
            }

            public void MoveSelection(int direction, bool wiggle = false) {
                int selection = Selection;
                direction = Math.Sign(direction);
                int num = 0;
                foreach (TextMenu.Item item in CurrentMenu) {
                    if (item.Hoverable)
                        num++;
                }

                do {
                    Selection += direction;
                    if (num > 2) {
                        if (Selection < 0) {
                            Selection = CurrentMenu.Count - 1;
                        } else if (Selection >= CurrentMenu.Count) {
                            Selection = 0;
                        }
                    } else if (Selection < 0 || Selection > CurrentMenu.Count - 1) {
                        Selection = Calc.Clamp(Selection, 0, CurrentMenu.Count - 1);
                        break;
                    }
                }
                while (!Current.Hoverable);

                if (!Current.Hoverable) {
                    Selection = selection;
                }
                if (Selection != selection && Current != null) {
                    if (selection >= 0 && CurrentMenu[selection] != null && CurrentMenu[selection].OnLeave != null) {
                        CurrentMenu[selection].OnLeave();
                    }
                    Current.OnEnter?.Invoke();
                    if (wiggle) {
                        Audio.Play((direction > 0) ? "event:/ui/main/rollover_down" : "event:/ui/main/rollover_up");
                        Current.SelectWiggler.Start();
                    }
                }
            }

            public void RecalculateSize() {
                TitleHeight = ActiveFont.LineHeight;

                LeftColumnWidth = RightColumnWidth = _MenuHeight = 0f;
                if (Menus.Count < 1 || CurrentMenu == null)
                    return;

                foreach (TextMenu.Item item in CurrentMenu) {
                    if (item.IncludeWidthInMeasurement) {
                        LeftColumnWidth = Math.Max(LeftColumnWidth, item.LeftWidth());
                    }
                }
                foreach (TextMenu.Item item in CurrentMenu) {
                    if (item.IncludeWidthInMeasurement) {
                        RightColumnWidth = Math.Max(RightColumnWidth, item.RightWidth());
                    }
                }
                foreach (TextMenu.Item item in CurrentMenu) {
                    if (item.Visible) {
                        _MenuHeight += item.Height() + Container.ItemSpacing;
                    }
                }
                _MenuHeight -= Container.ItemSpacing;
            }

            public float GetYOffsetOf(TextMenu.Item item) {
                if (item == null) {
                    return 0f;
                }
                float offset = 0f;
                foreach (TextMenu.Item item2 in CurrentMenu) {
                    if (item2.Visible) {
                        offset += item2.Height() + ItemSpacing;
                    }
                    if (item2 == item) {
                        break;
                    }
                }
                return offset - item.Height() * 0.5f - ItemSpacing + Container.GetYOffsetOf(this);
            }

            private void menu_Added() {
                foreach (Tuple<string, List<TextMenu.Item>> menu in delayedAddMenus) {
                    Add(menu.Item1, menu.Item2);
                }
                MenuIndex = InitialSelection;
            }

            private void menu_Update() {
                OnUpdate?.Invoke();
                //ease check needed to eat the first input from this.Container
                if (Focused) {
                    if (!wasFocused) {
                        wasFocused = true;
                        goto AfterInput;
                    }
                    if (Input.MenuDown.Pressed) {
                        if (!Input.MenuDown.Repeating || Selection != LastPossibleSelection) {
                            MoveSelection(1, true);
                        }
                    } else if (Input.MenuUp.Pressed && (!Input.MenuUp.Repeating || Selection != FirstPossibleSelection)) {
                        MoveSelection(-1, true);
                    }
                    if (Current != null) {
                        if (Input.MenuLeft.Pressed) {
                            Current.LeftPressed();
                        }
                        if (Input.MenuRight.Pressed) {
                            Current.RightPressed();
                        }
                        if (Input.MenuConfirm.Pressed) {
                            Current.ConfirmPressed();
                            Current.OnPressed?.Invoke();
                        }
                        if (Input.MenuJournal.Pressed && Current.OnAltPressed != null) {
                            Current.OnAltPressed();
                        }
                    }
                    if (!Input.MenuConfirm.Pressed) {
                        if (Input.MenuCancel.Pressed || Input.ESC.Pressed || Input.Pause.Pressed) {
                            Focused = false;
                            Audio.Play("event:/ui/main/button_back");
                            Container.AutoScroll = containerAutoScroll;
                            Container.Focused = true;
                        }
                    }
                } else
                    wasFocused = false;
                AfterInput:
                foreach (TextMenu.Item item in CurrentMenu) {
                    item.OnUpdate?.Invoke();
                    item.Update();
                }
                if (Settings.Instance.DisableFlashes) {
                    HighlightColor = TextMenu.HighlightColorA;
                } else if (Engine.Scene.OnRawInterval(0.1f)) {
                    if (HighlightColor == TextMenu.HighlightColorA) {
                        HighlightColor = TextMenu.HighlightColorB;
                    } else {
                        HighlightColor = TextMenu.HighlightColorA;
                    }
                }
                if (containerAutoScroll) {
                    if (Container.Height > Container.ScrollableMinSize) {
                        Container.Position.Y += (ScrollTargetY - Container.Position.Y) * (1f - (float) Math.Pow(0.01f, Engine.RawDeltaTime));
                        return;
                    }
                    Container.Position.Y = 540f;
                }
            }
            private void menu_Render(Vector2 position) {
                RecalculateSize();
                foreach (TextMenu.Item item in CurrentMenu) {
                    if (item.Visible) {
                        float itemHeight = item.Height();
                        Vector2 vector = position + new Vector2(0f, itemHeight * 0.5f + item.SelectWiggler.Value * 8f);
                        if (vector.Y + itemHeight * 0.5f > 0f && vector.Y - itemHeight * 0.5f < Engine.Height) {
                            item.Render(vector, Focused && Current == item);
                        }
                        position.Y += itemHeight + ItemSpacing;
                    }
                }
            }

            #endregion

            #region TextMenu.Item

            public OptionSubMenu Change(Action<int> onValueChange) {
                OnValueChange = onValueChange;
                return this;
            }

            public override void LeftPressed() {
                if (MenuIndex > 0) {
                    Audio.Play("event:/ui/main/button_toggle_off");
                    MenuIndex--;
                    lastDir = -1;
                    ValueWiggler.Start();
                    FirstSelection();
                    OnValueChange?.Invoke(MenuIndex);
                }
            }

            public override void RightPressed() {
                if (MenuIndex < Menus.Count - 1) {
                    Audio.Play("event:/ui/main/button_toggle_on");
                    MenuIndex++;
                    lastDir = 1;
                    ValueWiggler.Start();
                    FirstSelection();
                    OnValueChange?.Invoke(MenuIndex);
                }
            }

            public override void ConfirmPressed() {
                if (CurrentMenu.Count > 0) {
                    containerAutoScroll = Container.AutoScroll;
                    Container.AutoScroll = false;
                    Container.Focused = false;
                    Focused = true;
                    FirstSelection();
                }
            }

            public override float LeftWidth() {
                return ActiveFont.Measure(Label).X;
            }

            public override float RightWidth() {
                float width = 0f;
                foreach (string menu in Menus.Select(tuple => tuple.Item1)) {
                    width = Math.Max(width, ActiveFont.Measure(menu).X);
                }
                return width + 120f;
            }

            public override float Height() {
                //If there are no items, MenuHeight will actually be a negative number
                if (CurrentMenu != null && CurrentMenu.Count > 0)
                    return TitleHeight + (MenuHeight);
                else
                    return TitleHeight;
            }

            public override void Added() {
                base.Added();
                menu_Added();
            }

            public override void Update() {
                MenuHeight = Calc.Approach(MenuHeight, _MenuHeight, Engine.DeltaTime * Math.Abs(MenuHeight - _MenuHeight) * 8f);

                sine += Engine.RawDeltaTime;
                base.Update();
                if (CurrentMenu != null)
                    menu_Update();
            }

            public override void Render(Vector2 position, bool highlighted) {
                Vector2 top = new Vector2(position.X, position.Y - (Height() / 2));

                float alpha = Container.Alpha;
                Color color = Disabled ? Color.DarkSlateGray : ((highlighted ? Container.HighlightColor : Color.White) * alpha);
                Color strokeColor = Color.Black * (alpha * alpha * alpha);

                bool uncentered = Container.InnerContent == TextMenu.InnerContentMode.TwoColumn && !AlwaysCenter;

                Vector2 titlePosition = top + (Vector2.UnitY * TitleHeight / 2) + (uncentered ? Vector2.Zero : new Vector2(Container.Width * 0.5f, 0f));
                Vector2 justify = uncentered ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
                Vector2 iconJustify = uncentered ? new Vector2(ActiveFont.Measure(Label).X + Icon.Width, 5f) : new Vector2(ActiveFont.Measure(Label).X / 2 + Icon.Width, 5f);
                OptionSubMenu.DrawIcon(titlePosition, Icon, iconJustify, true, (Disabled || CurrentMenu?.Count < 1 ? Color.DarkSlateGray : (Focused ? Container.HighlightColor : Color.White)) * alpha, 0.8f);
                ActiveFont.DrawOutline(Label, titlePosition, justify, Vector2.One, color, 2f, strokeColor);
                if (Menus.Count > 0) {
                    float rWidth = RightWidth();

                    ActiveFont.DrawOutline(Menus[MenuIndex].Item1, titlePosition + new Vector2(Container.Width - rWidth * 0.5f + lastDir * ValueWiggler.Value * 8f, 0f), new Vector2(0.5f, 0.5f), Vector2.One * 0.8f, color, 2f, strokeColor);
                    Vector2 vector = Vector2.UnitX * (highlighted ? ((float) Math.Sin(sine * 4f) * 4f) : 0f);
                    Color color2 = MenuIndex > 0 ? color : (Color.DarkSlateGray * alpha);
                    Vector2 position2 = titlePosition + new Vector2(Container.Width - rWidth + 40f + ((lastDir < 0) ? (-ValueWiggler.Value * 8f) : 0f), 0f) - (MenuIndex > 0 ? vector : Vector2.Zero);
                    ActiveFont.DrawOutline("<", position2, new Vector2(0.5f, 0.5f), Vector2.One, color2, 2f, strokeColor);

                    bool flag2 = MenuIndex < Menus.Count - 1;
                    Color color3 = flag2 ? color : (Color.DarkSlateGray * alpha);
                    Vector2 position3 = titlePosition + new Vector2(Container.Width - 40f + ((lastDir > 0) ? (ValueWiggler.Value * 8f) : 0f), 0f) + (flag2 ? vector : Vector2.Zero);
                    ActiveFont.DrawOutline(">", position3, new Vector2(0.5f, 0.5f), Vector2.One, color3, 2f, strokeColor);
                }


                if (CurrentMenu != null) {
                    Vector2 menuPosition = new Vector2(top.X + ItemIndent, top.Y + TitleHeight + ItemSpacing);
                    menu_Render(menuPosition);
                }
            }

            private static void DrawIcon(Vector2 position, MTexture icon, Vector2 justify, bool outline, Color color, float scale) {
                if (outline) {
                    icon.DrawOutlineCentered(position + justify, color);
                } else {
                    icon.DrawCentered(position + justify, color, scale);
                }
            }

            #endregion

        }
    }
}
