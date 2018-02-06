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
    public static class TextMenuExt {

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
                icon.DrawOutlineCentered(new Vector2(position.X + width * 0.5f, position.Y + height * 0f), color, scale);
            else
                icon.DrawCentered(new Vector2(position.X + width * 0.5f, position.Y + height * 0f), color, scale);

            textPosition.X += inputWidth ?? (icon.Width * scale);
        }

        public interface IItemExt {

            string Icon { get; set; }
            float? IconWidth { get; set; }
            bool IconOutline { get; set; }

            Vector2 Offset { get; set; }
            float Alpha { get; set; }

        }

        public class ButtonExt : TextMenu.Button, IItemExt {

            public string Icon { get; set; }
            public float? IconWidth { get; set; }
            public bool IconOutline { get; set; }

            public Vector2 Offset { get; set; }
            public float Alpha { get; set; }

            public ButtonExt(string label, string icon = null)
                : base(label) {
                Icon = icon;
            }

            public override void Render(Vector2 position, bool highlighted) {
                position += Offset;
                float alpha = Container.Alpha * Alpha;

                Color textColor = (Disabled ? Color.DarkSlateGray : highlighted ? Container.HighlightColor : Color.White) * alpha;
                Color strokeColor = Color.Black * (alpha * alpha * alpha);

                bool flag = Container.InnerContent == TextMenu.InnerContentMode.TwoColumn && !AlwaysCenter;

                Vector2 textPosition = position + (flag ? Vector2.Zero : new Vector2(Container.Width * 0.5f, 0f));
                Vector2 justify = (flag && !AlwaysCenter) ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);

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

            public string Icon { get; set; }
            public float? IconWidth { get; set; }
            public bool IconOutline { get; set; }

            public Vector2 Offset { get; set; }
            public float Alpha { get; set; }

            public SubHeaderExt(string title, string icon = null)
                : base(title) {
                Icon = icon;
            }

            public override void Render(Vector2 position, bool highlighted) {
                position += Offset;
                float alpha = Container.Alpha * Alpha;

                Color strokeColor = Color.Black * (alpha * alpha * alpha);

                Vector2 textPosition = position + (Container.InnerContent == TextMenu.InnerContentMode.TwoColumn ? new Vector2(0f, 32f) : new Vector2(Container.Width * 0.5f, 32f));
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

                if (Title.Length < 0)
                    return;

                ActiveFont.DrawOutline(Title, textPosition, justify, Vector2.One * 0.6f, Color.Gray * alpha, 2f, strokeColor);
            }

        }

    }
}
