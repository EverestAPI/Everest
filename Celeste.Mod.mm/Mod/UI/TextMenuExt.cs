using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using static Celeste.patch_TextMenu;

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
            public Vector2 Scale { get; set; } = Vector2.One;

            public override float Height() => base.Height() * Scale.Y;
            public override float LeftWidth() => base.LeftWidth() * Scale.X;

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

                ActiveFont.DrawOutline(Label, textPosition, justify, Scale, textColor, 2f, strokeColor);
            }

        }

        public class SubHeaderExt : patch_TextMenu.patch_SubHeader, IItemExt {

            public Color TextColor { get; set; } = Color.Gray;

            public string Icon { get; set; }
            public float? IconWidth { get; set; }
            public bool IconOutline { get; set; }

            public Vector2 Offset { get; set; }
            public float Alpha { get; set; } = 1f;
            public bool AlwaysCenter { get; set; }

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
                    Container.InnerContent == TextMenu.InnerContentMode.TwoColumn && !AlwaysCenter ?
                    new Vector2(0f, MathHelper.Max(0f, 32f - 48f + HeightExtra)) :
                    new Vector2(Container.Width * 0.5f, MathHelper.Max(0f, 32f - 48f + HeightExtra))
                );
                Vector2 justify = new Vector2(Container.InnerContent == TextMenu.InnerContentMode.TwoColumn && !AlwaysCenter ? 0f : 0.5f, 0.5f);

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
                    uneasedAlpha = Calc.Approach(uneasedAlpha, targetAlpha, Engine.RawDeltaTime * 3f);

                    if (FadeVisible)
                        Alpha = Ease.SineOut(uneasedAlpha);
                    else
                        Alpha = Ease.SineIn(uneasedAlpha);
                }

                Visible = (Alpha != 0);
            }
        }

        /// <summary>
        /// Convenience class for creating a <see cref="TextMenu.Option{T}"/> from an <see cref="Enum"/>.<br></br>
        /// Not to be confused with <see cref="EnumerableSlider{T}"/>
        /// </summary>
        /// <typeparam name="T">Enum Type</typeparam>
        public class EnumSlider<T> : TextMenu.Option<T> where T : Enum {

            /// <summary>
            /// Creates a new <see cref="EnumSlider{T}"/>
            /// </summary>
            /// <param name="label">Slider label (defaults to enum name)</param>
            /// <param name="startValue">Initial value</param>
            public EnumSlider(string label = null, T startValue = default) : base(label ?? typeof(T).Name) {
                Array enumValues = Enum.GetValues(typeof(T));
                foreach (T value in enumValues) {
                    Add(value.ToString(), value, value.Equals(startValue));
                }
            }
        }

        /// <summary>
        /// Convenience class for creating a <see cref="TextMenu.Option{T}"/> from a <see cref="IEnumerable{T}"/>.<br></br>
        /// Not to be confused with <see cref="EnumSlider{T}"/>
        /// </summary>
        /// <typeparam name="T">Value Type</typeparam>
        public class EnumerableSlider<T> : TextMenu.Option<T> {

            /// <summary>
            /// Creates a new <see cref="EnumerableSlider{T}"/>
            /// </summary>
            /// <param name="label">Slider label</param>
            /// <param name="options"></param>
            /// <param name="startValue">Initial value</param>
            public EnumerableSlider(string label, IEnumerable<T> options, T startValue)
                : base(label) {
                foreach (T value in options) {
                    Add(value.ToString(), value, value.Equals(startValue));
                }
            }

            /// <summary>
            /// Creates a new <see cref="EnumerableSlider{T}"/>
            /// </summary>
            /// <param name="label">Slider label</param>
            /// <param name="options">IEnumerable containing <typeparamref name="T"/>, <see cref="string"/> pairs.</param>
            /// <param name="startValue">Initial value</param>
            public EnumerableSlider(string label, IEnumerable<KeyValuePair<T, string>> options, T startValue)
                : base(label) {
                foreach (KeyValuePair<T, string> kvp in options) {
                    Add(kvp.Value, kvp.Key, kvp.Key.Equals(startValue));
                }
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

            /// <inheritdoc cref="TextMenu.Option{T}.Change(Action{T})"/>
            public IntSlider Change(Action<int> action) {
                OnValueChange = action;
                return this;
            }

            public override void Added() {
                Container.InnerContent = TextMenu.InnerContentMode.TwoColumn;
            }

            public override void LeftPressed() {
                if (Input.MenuLeft.Repeating)
                    fastMoveTimer += Engine.RawDeltaTime * 8;
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
                    fastMoveTimer += Engine.RawDeltaTime * 8;
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
                // Measure Index in case it is externally set ouside the bounds
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

                    Vector2 vector = Vector2.UnitX * (float) (highlighted ? (Math.Sin(sine * 4f) * 4f) : 0f);

                    Vector2 position2 = position + new Vector2(Container.Width - rWidth + 40f + ((lastDir < 0) ? (-ValueWiggler.Value * 8f) : 0f), 0f) - (Index > min ? vector : Vector2.Zero);
                    ActiveFont.DrawOutline("<", position2, new Vector2(0.5f, 0.5f), Vector2.One, Index > min ? color : (Color.DarkSlateGray * alpha), 2f, strokeColor);

                    position2 = position + new Vector2(Container.Width - 40f + ((lastDir > 0) ? (ValueWiggler.Value * 8f) : 0f), 0f) + (Index < max ? vector : Vector2.Zero);
                    ActiveFont.DrawOutline(">", position2, new Vector2(0.5f, 0.5f), Vector2.One, Index < max ? color : (Color.DarkSlateGray * alpha), 2f, strokeColor);
                }
            }
        }

        /// <summary>
        /// <see cref="TextMenu.Item"/> that acts as a Submenu for other Items.
        /// <br/><br/>
        /// Currently does not support recursive submenus
        /// </summary>
        public class SubMenu : patch_Item {
            public string Label;
            MTexture Icon;

            /// <inheritdoc cref="patch_TextMenu.Items"/>
            public List<TextMenu.Item> Items { get; private set; }

            private List<TextMenu.Item> delayedAddItems;

            /// <inheritdoc cref="TextMenu.Selection"/>
            public int Selection;

            /// <inheritdoc cref="TextMenu.Current"/>
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

            /// <inheritdoc cref="TextMenu.FirstPossibleSelection"/>
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

            /// <inheritdoc cref="TextMenu.LastPossibleSelection"/>
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

            /// <inheritdoc cref="TextMenu.ScrollTargetY"/>
            public float ScrollTargetY {
                get {
                    float min = Engine.Height - 150f - Container.Height * Container.Justify.Y;
                    float max = 150f + Container.Height * Container.Justify.Y;
                    return Calc.Clamp((Engine.Height / 2) + Container.Height * Container.Justify.Y - GetYOffsetOf(Current), min, max);
                }
            }

            /// <inheritdoc cref="TextMenu.ItemSpacing"/>
            public float ItemSpacing;
            public float ItemIndent;
            /// <inheritdoc cref="TextMenu.HighlightColor"/>
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
            /// Create a new SubMenu.
            /// </summary>
            /// <param name="label"></param>
            /// <param name="enterOnSelect">Expand submenu when selected</param>
            public SubMenu(string label, bool enterOnSelect) : base() {
                // Item Constructor
                ConfirmSfx = SFX.ui_main_button_select;
                Label = label;
                Icon = GFX.Gui["downarrow"];
                Selectable = true;
                IncludeWidthInMeasurement = true;

                this.enterOnSelect = enterOnSelect;

                OnEnter = delegate {
                    if (this.enterOnSelect) {
                        ConfirmPressed();
                    }
                };


                // Menu Constructor
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

            public bool ContainsDelayedAddItem(TextMenu.Item item) {
                return Container == null && delayedAddItems.Contains(item);
            }

            public SubMenu InsertDelayedAddItem(TextMenu.Item item, TextMenu.Item after) {
                if (Container == null && delayedAddItems.Contains(after))
                    delayedAddItems.Insert(delayedAddItems.IndexOf(after) + 1, item);
                return this;
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

            /// <inheritdoc cref="TextMenu.Clear"/>
            public void Clear() {
                Items = new List<TextMenu.Item>();
            }

            /// <inheritdoc cref="TextMenu.IndexOf(TextMenu.Item)"/>
            public int IndexOf(TextMenu.Item item) {
                return Items.IndexOf(item);
            }

            /// <inheritdoc cref="TextMenu.FirstSelection"/>
            public void FirstSelection() {
                Selection = -1;
                MoveSelection(1, false);
            }

            /// <summary>
            /// Set the selection to the last possible <see cref="TextMenu.Item"/>.
            /// </summary>
            public void LastSelection() {
                Selection = LastPossibleSelection;
                MoveSelection(0, false);
            }

            /// <inheritdoc cref="TextMenu.MoveSelection(int, bool)"/>
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
                    if (enterOnSelect) {
                        if (Selection < 0 || Selection >= Items.Count) {
                            // Avoid crash when getting Current item
                            Selection = selection;
                            Exit();
                            Container.MoveSelection(direction, true);
                            return;
                        }
                    }
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
                        Audio.Play(direction > 0 ? SFX.ui_main_roll_down : SFX.ui_main_roll_up);
                        Current.SelectWiggler.Start();
                    }
                }
            }

            /// <inheritdoc cref="TextMenu.RecalculateSize"/>
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

            /// <inheritdoc cref="TextMenu.GetYOffsetOf(TextMenu.Item)"/>
            public float GetYOffsetOf(TextMenu.Item item) {
                float offset = Container.GetYOffsetOf(this) - Height() * 0.5f;
                if (item == null) {
                    // common case is all items in submenu are disabled when item is null
                    return offset + TitleHeight * 0.5f;
                }
                offset += TitleHeight;
                foreach (TextMenu.Item child in Items) {
                    if (child.Visible) {
                        offset += child.Height() + ItemSpacing;
                    }
                    if (child == item) {
                        break;
                    }
                }
                return offset - item.Height() * 0.5f - ItemSpacing;
            }

            public void Exit() {
                Current?.OnLeave?.Invoke();
                Focused = false;
                if (!Input.MenuUp.Repeating && !Input.MenuDown.Repeating)
                    Audio.Play(SFX.ui_main_button_back);
                Container.AutoScroll = containerAutoScroll;
                Container.Focused = true;
            }

            public override string SearchLabel() {
                return Label;
            }

            #endregion

            #region TextMenu.Item

            public override void ConfirmPressed() {
                if (Items.Count > 0) {
                    Container.Focused = false;
                    Focused = true;
                    if (Input.MenuUp.Pressed)
                        LastSelection();
                    else
                        FirstSelection();
                    containerAutoScroll = Container.AutoScroll;
                    Container.AutoScroll = false;
                    if (!Input.MenuUp.Repeating && !Input.MenuDown.Repeating)
                        Audio.Play(ConfirmSfx);
                    base.ConfirmPressed();
                }
            }

            public override float LeftWidth() {
                return ActiveFont.Measure(Label).X;
            }

            public override float RightWidth() {
                return Icon.Width;
            }

            public override float Height() {
                // If there are no items, MenuHeight will actually be a negative number
                if (Items.Count > 0)
                    return TitleHeight + (MenuHeight * Ease.QuadOut(ease));
                else
                    return TitleHeight;
            }

            public override void Added() {
                base.Added();
                foreach (TextMenu.Item item in delayedAddItems) {
                    Add(item);
                }
            }

            public override void Update() {
                if (Focused)
                    ease = Calc.Approach(ease, 1f, Engine.RawDeltaTime * 4f);
                else
                    ease = Calc.Approach(ease, 0f, Engine.RawDeltaTime * 4f);
                base.Update();

                // ease check needed to eat the first input from Container
                if (Focused && ease > 0.9f) {
                    if (Input.MenuDown.Pressed && (!Input.MenuDown.Repeating || Selection != LastPossibleSelection || enterOnSelect)) {
                        MoveSelection(1, true);
                    } else if (Input.MenuUp.Pressed && (!Input.MenuUp.Repeating || Selection != FirstPossibleSelection || enterOnSelect)) {
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
                            Exit();
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

                if (Focused && containerAutoScroll) {
                    if (Container.Height > Container.ScrollableMinSize) {
                        Container.Position.Y += (ScrollTargetY - Container.Position.Y) * (1f - (float) Math.Pow(0.01f, Engine.RawDeltaTime));
                        return;
                    }
                    Container.Position.Y = 540f;
                }
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
                    RecalculateSize();
                    foreach (TextMenu.Item item in Items) {
                        if (item.Visible) {
                            float height = item.Height();
                            Vector2 itemPosition = menuPosition + new Vector2(0f, height * 0.5f + item.SelectWiggler.Value * 8f);
                            if (itemPosition.Y + height * 0.5f > 0f && itemPosition.Y - height * 0.5f < Engine.Height) {
                                item.Render(itemPosition, Focused && Current == item);
                            }
                            menuPosition.Y += height + ItemSpacing;
                        }
                    }
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
		/// <br/><br/>
		/// Currently does not support recursive submenus
		/// </summary>
		public class OptionSubMenu : patch_Item {
            public string Label;
            MTexture Icon;

            // Menus are stored as lists associated with a label
            public List<Tuple<string, List<TextMenu.Item>>> Menus { get; private set; }

            private List<Tuple<string, List<TextMenu.Item>>> delayedAddMenus;

            public int MenuIndex;

            private int InitialSelection;
            ///<inheritdoc cref="TextMenu.Selection"/>
            public int Selection;

            private int lastDir;
            private float sine;

            /// <summary>
            /// Invoked when the selected menu is changed.
            /// </summary>
            public Action<int> OnValueChange;

            /// <summary>
            /// The selected set of <see cref="TextMenu.Item"/>s.
            /// </summary>
            public List<TextMenu.Item> CurrentMenu {
                get { return (Menus.Count > 0) ? Menus[MenuIndex].Item2 : null; }
            }

            /// <inheritdoc cref="TextMenu.Current"/>
            public TextMenu.Item Current {
                get {
                    if (CurrentMenu.Count <= 0 || Selection < 0) {
                        return null;
                    }
                    return CurrentMenu[Selection];
                }
            }

            /// <inheritdoc cref="TextMenu.FirstPossibleSelection"/>
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

            /// <inheritdoc cref="TextMenu.LastPossibleSelection"/>
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

            /// <inheritdoc cref="TextMenu.ScrollTargetY"/>
            public float ScrollTargetY {
                get {
                    float min = Engine.Height - 150 - Container.Height * Container.Justify.Y;
                    float max = 150f + Container.Height * Container.Justify.Y;
                    return Calc.Clamp(Engine.Height / 2 + Container.Height * Container.Justify.Y - GetYOffsetOf(Current), min, max);
                }
            }

            /// <inheritdoc cref="TextMenu.ItemSpacing"/>
            public float ItemSpacing;
            public float ItemIndent;

            private Color HighlightColor;

            public string ConfirmSfx;

            public bool AlwaysCenter;

            public float LeftColumnWidth;
            public float RightColumnWidth;

            public float TitleHeight { get; private set; }

            // Accessor property used to smootly(ish) transition between menu heights
            public float MenuHeight { get; private set; }
            private float _MenuHeight;

            public bool Focused;
            private bool wasFocused;

            private bool containerAutoScroll;

            public OptionSubMenu(string label) : base() {
                // Item Constructor
                ConfirmSfx = SFX.ui_main_button_select;
                Label = label;
                Icon = GFX.Gui["downarrow"];

                Selectable = true;
                IncludeWidthInMeasurement = true;

                MenuIndex = 0;


                // Menu Constructor
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

            /// <inheritdoc cref="TextMenu.FirstSelection"/>
            public void FirstSelection() {
                Selection = -1;
                if (CurrentMenu.Count > 0)
                    MoveSelection(1, true);
            }

            /// <inheritdoc cref="TextMenu.MoveSelection(int, bool)"/>
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
                float offset = Container.GetYOffsetOf(this) - Height() * 0.5f;
                if (item == null) {
                    // common case is all items in submenu are disabled when item is null
                    return offset + TitleHeight * 0.5f;
                }
                offset += TitleHeight;
                foreach (TextMenu.Item child in CurrentMenu) {
                    if (child.Visible) {
                        offset += child.Height() + ItemSpacing;
                    }
                    if (child == item) {
                        break;
                    }
                }
                return offset - item.Height() * 0.5f - ItemSpacing;
            }

            public override string SearchLabel() {
                return Label;
            }

            #endregion

            #region TextMenu.Item

            /// <summary>
            /// Set the action to be invoked when the selected menu is changed.
            /// </summary>
            public OptionSubMenu Change(Action<int> onValueChange) {
                OnValueChange = onValueChange;
                return this;
            }

            public override void LeftPressed() {
                if (MenuIndex > 0) {
                    Audio.Play(SFX.ui_main_button_toggle_off);
                    MenuIndex--;
                    lastDir = -1;
                    ValueWiggler.Start();
                    FirstSelection();
                    OnValueChange?.Invoke(MenuIndex);
                }
            }

            public override void RightPressed() {
                if (MenuIndex < Menus.Count - 1) {
                    Audio.Play(SFX.ui_main_button_toggle_on);
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
                // If there are no items, MenuHeight will actually be a negative number
                return TitleHeight + Math.Max(MenuHeight, 0);
            }

            public override void Added() {
                base.Added();
                foreach (Tuple<string, List<TextMenu.Item>> menu in delayedAddMenus) {
                    Add(menu.Item1, menu.Item2);
                }
                MenuIndex = InitialSelection;
            }

            public override void Update() {
                MenuHeight = Calc.Approach(MenuHeight, _MenuHeight, Engine.RawDeltaTime * Math.Abs(MenuHeight - _MenuHeight) * 8f);

                sine += Engine.RawDeltaTime;
                base.Update();
                if (CurrentMenu != null) {
                    if (Focused) {
                        // ease check needed to eat the first input from this.Container
                        if (!wasFocused) {
                            wasFocused = true;
                            goto AfterInput;
                        }
                        if (Input.MenuDown.Pressed && (!Input.MenuDown.Repeating || Selection != LastPossibleSelection)) {
                            MoveSelection(1, true);
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
                                Current?.OnLeave?.Invoke();
                                Focused = false;
                                Audio.Play(SFX.ui_main_button_back);
                                Container.AutoScroll = containerAutoScroll;
                                Container.Focused = true;
                            }
                        }
                    } else
                        wasFocused = false;

                    AfterInput:
                    foreach (Tuple<string, List<TextMenu.Item>> menu in Menus) {
                        foreach (TextMenu.Item item in menu.Item2) {
                            item.OnUpdate?.Invoke();
                            item.Update();
                        }
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

                    if (Focused && containerAutoScroll) {
                        if (Container.Height > Container.ScrollableMinSize) {
                            Container.Position.Y += (ScrollTargetY - Container.Position.Y) * (1f - (float) Math.Pow(0.01f, Engine.RawDeltaTime));
                            return;
                        }
                        Container.Position.Y = 540f;
                    }
                }
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
                    Vector2 wiggle = Vector2.UnitX * (highlighted ? ((float) Math.Sin(sine * 4f) * 4f) : 0f);
                    Color arrowColor = MenuIndex > 0 ? color : (Color.DarkSlateGray * alpha);
                    Vector2 arrowPosition = titlePosition + new Vector2(Container.Width - rWidth + 40f + ((lastDir < 0) ? (-ValueWiggler.Value * 8f) : 0f), 0f) - (MenuIndex > 0 ? wiggle : Vector2.Zero);
                    ActiveFont.DrawOutline("<", arrowPosition, new Vector2(0.5f, 0.5f), Vector2.One, arrowColor, 2f, strokeColor);

                    arrowColor = MenuIndex < Menus.Count - 1 ? color : (Color.DarkSlateGray * alpha);
                    arrowPosition = titlePosition + new Vector2(Container.Width - 40f + ((lastDir > 0) ? (ValueWiggler.Value * 8f) : 0f), 0f) + (MenuIndex < Menus.Count - 1 ? wiggle : Vector2.Zero);
                    ActiveFont.DrawOutline(">", arrowPosition, new Vector2(0.5f, 0.5f), Vector2.One, arrowColor, 2f, strokeColor);
                }

                if (CurrentMenu != null) {
                    Vector2 itemPosition = new Vector2(top.X + ItemIndent, top.Y + TitleHeight + ItemSpacing);
                    float menuYOffset = itemPosition.Y;
                    RecalculateSize();
                    foreach (TextMenu.Item item in CurrentMenu) {
                        if (item.Visible) {
                            float itemHeight = item.Height();
                            Vector2 itemOffset = itemPosition + new Vector2(0f, itemHeight * 0.5f + item.SelectWiggler.Value * 8f);
                            if (itemOffset.Y - menuYOffset < MenuHeight && itemOffset.Y + itemHeight * 0.5f > 0f && itemOffset.Y - itemHeight * 0.5f < Engine.Height) {
                                item.Render(itemOffset, Focused && Current == item);
                            }
                            itemPosition.Y += itemHeight + ItemSpacing;
                        }
                    }
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

        public class BatchModeContext : IDisposable {

            patch_TextMenu menu;

            public BatchModeContext(patch_TextMenu menu) {
                menu.BatchMode = true;
                this.menu = menu;
            }

            public void Dispose() {
                menu.BatchMode = false;
            }
        }

        public class SubMenuWithInputs : TextMenu.Item, IItemExt {
            public Color TextColor { get; set; } = Color.Gray;
            public Color ButtonColor { get; set; } = Color.White;
            public Color StrokeColor { get; set; } = Color.White;
            public float Alpha { get; set; } = 1f;
            public float Scale { get; set; } = 0.6f;
            public string Icon { get; set; }
            public float? IconWidth { get; set; }
            public bool IconOutline { get; set; }
            public Vector2 Offset { get; set; }
            private readonly object[] Items;

            public SubMenuWithInputs(string text, char separator, VirtualButton[] buttons) {

                string[] parts = text.Split(separator);
                Items = new object[parts.Length * 2 - 1];

                for (int index = 0; index < Items.Length; index++) {
                    if (index % 2 == 0) {
                        // add text
                        Items[index] = parts[index / 2];
                    } else {
                        // add VirtualButton
                        Items[index] = buttons[index / 2];
                    }
                }
            }

            public override float Height() {
                return ActiveFont.LineHeight;
            }

            public override void Render(Vector2 position, bool highlighted) {
                Vector2 lineOffset = position;
                Vector2 justify = new(0f, 0.5f);
                float strokeAlpha = Alpha * Alpha * Alpha;


                foreach (object item in Items) {
                    if (item is string) {
                        ActiveFont.DrawOutline(item as string, lineOffset, justify, Vector2.One * Scale, TextColor * Alpha, 2f, Color.Black * strokeAlpha);
                        lineOffset.X += ActiveFont.Measure(item as string).X * Scale;
                    } else if (item is VirtualButton) {
                        VirtualButton virtualButton = item as VirtualButton;
                        MTexture buttonTexture;

                        if (Input.GuiInputController()) {
                            buttonTexture = Input.GuiButton(virtualButton, Input.PrefixMode.Attached);
                        } else if (virtualButton.Binding.Keyboard.Count > 0) {
                            buttonTexture = Input.GuiKey(virtualButton.Binding.Keyboard[0]);
                        } else {
                            buttonTexture = Input.GuiKey(Keys.None);
                        }

                        buttonTexture.DrawJustified(lineOffset, justify, ButtonColor * strokeAlpha, Scale);
                        lineOffset.X += buttonTexture.Width * Scale;
                    }
                }
            }
        }

        // TODO: this was copy pasted from EaseInSubHeaderExt, find a way to abstract away the EaseIn behavior
        public class EaseInSubMenuWithInputs : SubMenuWithInputs {
            public bool FadeVisible { get; set; } = true;
            private float uneasedAlpha;

            public EaseInSubMenuWithInputs(
                string text,
                char separator,
                VirtualButton[] buttons,
                bool initiallyVisible
            ) : base(text, separator, buttons) {
                FadeVisible = initiallyVisible;
                Alpha = FadeVisible ? 1 : 0;
                uneasedAlpha = Alpha;
            }

            public override float Height() {
                if (Container != null) {
                    return MathHelper.Lerp(-Container.ItemSpacing, base.Height(), Alpha);
                } else {
                    return base.Height();
                }
            }

            public override void Update() {
                base.Update();

                // gradually make the sub-header fade in or out. (~333ms fade)
                float targetAlpha = FadeVisible ? 1 : 0;
                if (uneasedAlpha != targetAlpha) {
                    uneasedAlpha = Calc.Approach(uneasedAlpha, targetAlpha, Engine.RawDeltaTime * 3f);

                    if (FadeVisible)
                        Alpha = Ease.SineOut(uneasedAlpha);
                    else
                        Alpha = Ease.SineIn(uneasedAlpha);
                }

                Visible = Alpha != 0;
            }
        }

        public class TextBox : TextMenu.Item {
            private static readonly float DEFAULT_TEXT_SCALE = 1.10f;

            public delegate void OnTextChangeHandler(string text);
            public event OnTextChangeHandler OnTextChange;

            public string PlaceholderText;

            private string _text = "";
            public string Text {
                get => _text; protected set {
                    _text = value;
                    OnTextChange?.Invoke(Text);
                }
            }

            public float Alpha { get; set; } = 1;
            public Color TextColor { get; set; } = Color.White;
            public Vector2 TextJustify { get; set; } = new Vector2(0f, 0.5f);
            public float StrokeSize { get; set; } = 2f;
            public Color StrokeColor { get; set; } = Color.Black;
            public Color PlaceHolderTextColor { get; set; } = Color.LightGray * 0.75f;
            public Color TextBoxColor { get; set; } = Color.DarkSlateGray * 0.8f;
            public Vector2 TextScale { get; set; } = Vector2.One * DEFAULT_TEXT_SCALE;
            public Vector2 TextPadding { get; set; } = new Vector2(ActiveFont.Measure(' ').X * DEFAULT_TEXT_SCALE, ActiveFont.LineHeight * DEFAULT_TEXT_SCALE / 6);
            public float WidthScale { get; set; } = 1;
            public bool TextBoxConsumedInput { get; private set; } = false;

            public Dictionary<char, Action<TextBox>> OnTextInputCharActions = new() {
                {'\b', (textBox) => {
                    if(textBox.DeleteCharacter()) {
                        Audio.Play(SFX.ui_main_rename_entry_backspace);
                    }  else {
                        Audio.Play(SFX.ui_main_button_invalid);
                    }
                }},
            };

            public bool Typing { get; private set; } = false;

            public Action AfterInputConsumed;

            private Overworld overworld;
            private bool previousMountainAllowUserRotation;
            private bool previousEngineCommandsEnabled;

            private readonly Queue<char> inputQueue = new();


            public TextBox() {
                Selectable = true;
            }

            public TextBox(Overworld overworld) {
                Selectable = true;
                this.overworld = overworld;
            }

            public override float LeftWidth() {
                if (Container != null) {
                    return Container.Width * WidthScale;
                }
                return 0;
            }

            public override float Height() {
                return (ActiveFont.LineHeight * TextScale.Y) + (TextPadding.Y * 2);
            }

            public override void Render(Vector2 position, bool highlighted) {
                Vector2 textPosition = new(position.X + TextPadding.X, position.Y + (Height() / 2));

                Draw.Rect(position, Width, Height(), TextBoxColor);

                if (Text.Length <= 0 && !string.IsNullOrEmpty(PlaceholderText)) {
                    Vector2 placeholderSize = ActiveFont.Measure(PlaceholderText) * TextScale;
                    float overflowScale = Math.Min((Width - TextPadding.X * 4) / placeholderSize.X, 1f);

                    ActiveFont.DrawOutline(
                        PlaceholderText,
                        textPosition,
                        TextJustify,
                        TextScale * overflowScale,
                        PlaceHolderTextColor * Alpha,
                        StrokeSize * overflowScale,
                        StrokeColor * (Alpha * Alpha * Alpha)
                    );
                } else {
                    ActiveFont.DrawOutline(
                        Text + (Typing ? "_" : ""),
                        textPosition,
                        TextJustify,
                        TextScale,
                        TextColor * Alpha,
                        StrokeSize,
                        StrokeColor * (Alpha * Alpha * Alpha)
                    );
                }

            }

            public override void ConfirmPressed() {
                StartTyping();
            }

            public void ClearText() {
                Text = "";
            }

            public bool DeleteCharacter() {
                if (Text.Length > 0) {
                    Text = Text.Remove(Text.Length - 1);
                    return true;
                } else {
                    return false;
                }
            }

            public void StartTyping() {
                if (!Typing) {
                    Audio.Play(SFX.ui_main_button_toggle_on);
                    Typing = true;
                    Container.Focused = false;
                    ((patch_TextMenu) Container).RenderAsFocused = true;

                    previousEngineCommandsEnabled = Engine.Commands.Enabled;
                    Engine.Commands.Enabled = false;

                    if (overworld != null) {
                        previousMountainAllowUserRotation = overworld.Mountain.AllowUserRotation;
                        overworld.Mountain.AllowUserRotation = false;
                    }

                    inputQueue.Clear();
                    TextInput.OnInput += OnTextInput;
                }
            }

            public void StopTyping() {
                if (Typing) {
                    TextInput.OnInput -= OnTextInput;
                    inputQueue.Clear();


                    Audio.Play(SFX.ui_main_button_toggle_off);
                    Typing = false;
                    Container.Focused = true;
                    ((patch_TextMenu) Container).RenderAsFocused = false;
                    TextBoxConsumedInput = false;
                    MInput.Disabled = false;
                    Engine.Commands.Enabled = previousEngineCommandsEnabled;

                    if (overworld != null) {
                        overworld.Mountain.AllowUserRotation = previousMountainAllowUserRotation;
                    }
                }
            }

            private bool HandleNewInputChar(char c) {
                Vector2 newTextSize = ActiveFont.Measure(Text + c + "_") * TextScale;
                Vector2 totalTextPadding = TextPadding * 4;

                if (!char.IsControl(c) && ActiveFont.FontSize.Characters.ContainsKey(c) && (newTextSize + totalTextPadding).X < Width) {
                    Text += c;
                    Audio.Play(SFX.ui_main_rename_entry_char);
                    return true;
                } else {
                    Audio.Play(SFX.ui_main_button_invalid);
                    return false;
                }
            }

            public void OnTextInput(char c) {
                if (Typing) {
                    // This method will be called outside of the normal game Update cycle
                    // we use this queue to processes inputs in the main Update cycle
                    inputQueue.Enqueue(c);
                }
            }

            public override void Update() {
                while (inputQueue.Count > 0 && Typing) {
                    char c = inputQueue.Dequeue();
                    if (OnTextInputCharActions.TryGetValue(c, out Action<TextBox> action)) {
                        action(this);
                        TextBoxConsumedInput = true;
                    } else {
                        TextBoxConsumedInput = HandleNewInputChar(c);
                    }
                }

                if (Typing) {
                    if (MInput.Keyboard.Pressed(Keys.Delete)) {
                        if (Text.Length > 0) {
                            ClearText();
                            Audio.Play(SFX.ui_main_rename_entry_backspace);
                        }
                        TextBoxConsumedInput = true;
                    }


                    // We need to disable all other inputs if the textBox consumed that an input,
                    MInput.Disabled = TextBoxConsumedInput;
                    if (TextBoxConsumedInput) {
                        // Because we can only control the value of MInput.Disable for the duration of the paused menu Update
                        // we have to consume all the button presses to emulate disabling MInput for the rest of the Update call
                        foreach (VirtualInput input in patch_MInput.VirtualInputs) {
                            if (input is VirtualButton button) {
                                button.ConsumePress();
                            }
                        }
                    }


                    // ensure the player never enters free cam while typing, so to cover the case our Update() gets called we consume the input
                    // and if we get called afterwards we set ToggleMountainFreeCam to false before the next Render() call to MountainRenderer
                    ((patch_MountainRenderer) overworld?.Mountain)?.SetFreeCam(false);

                    AfterInputConsumed?.Invoke();
                }

                TextBoxConsumedInput = false;
            }

            private static int NegativeModulo(int number, int modulo) {
                return (number % modulo + modulo) % modulo;
            }

            public static bool WrappingLinearSearch<T>(List<T> items, Func<T, bool> predicate, int startIndex, bool inReverse, out int nextModIndex) {
                int step = inReverse ? -1 : 1;
                int targetIndex = NegativeModulo(startIndex - step, items.Count);

                for (int currentIndex = NegativeModulo(startIndex, items.Count); currentIndex != targetIndex; currentIndex = NegativeModulo(currentIndex + step, items.Count)) {
                    if (predicate(items[currentIndex])) {
                        nextModIndex = currentIndex;
                        return true;
                    }
                }

                nextModIndex = startIndex;
                return false;
            }
        }

        public class Modal : patch_Item {
            public Color BoxBorderColor { get; set; } = Color.White;
            public Color BoxBackgroundColor { get; set; } = Color.Black * 0.8f;
            public readonly TextMenu.Item Item;
            public int BorderThickness { get; set; } = 2;
            private readonly float? absoluteY;
            private readonly float? absoluteX;

            public Modal(TextMenu.Item item, float? absoluteX, float? absoluteY) {
                AboveAll = true;
                Visible = false;
                IncludeWidthInMeasurement = false;
                this.absoluteY = absoluteY;
                this.absoluteX = absoluteX;
                Item = item;
            }

            public override void Added() {
                base.Added();
                Item.Container = Container;
                Item.Added();
            }

            public override void Update() {
                base.Update();
                Item.OnUpdate?.Invoke();
                Item.Update();
            }

            public override bool AlwaysRender => true;

            public override float Height() {
                if (Container != null) {
                    return Container.ItemSpacing * -1;
                } else {
                    return 0;
                }
            }

            public override void Render(Vector2 position, bool highlighted) {
                Vector2 renderPosition = new(absoluteX ?? position.X, absoluteY ?? position.Y);
                for (int i = 1; i <= BorderThickness; i++) {
                    Draw.HollowRect(renderPosition.X - i, renderPosition.Y - i, Item.Width + (2 * i), Item.Height() + (2 * i), BoxBorderColor * Container.Alpha);
                }

                Item.Render(renderPosition, highlighted);
            }
        }

        public class SearchToolTip : patch_Item {
            public Vector2 preferredRenderLocation = new(100f, 952f);

            private readonly MTexture searchIcon = GFX.Gui["menu/mapsearch"];

            public SearchToolTip() {
                AboveAll = true;
                Selectable = false;
                IncludeWidthInMeasurement = false;
            }

            public override bool AlwaysRender => true;

            public override void Render(Vector2 position, bool highlighted) {
                float spaceNearMenu = (Engine.Width - Container.Width) / 2;
                float scaleFactor = Math.Min(spaceNearMenu / (preferredRenderLocation.X + searchIcon.Width / 2), 1);
                Vector2 searchIconLocation = new(preferredRenderLocation.X * scaleFactor, preferredRenderLocation.Y);
                searchIcon.DrawCentered(searchIconLocation, Color.White, scaleFactor);
                Input.GuiKey(Input.FirstKey(Input.QuickRestart)).Draw(searchIconLocation, Vector2.Zero, Color.White, scaleFactor);
            }
        }
    }
}
