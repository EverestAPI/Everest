using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
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
    }
}
