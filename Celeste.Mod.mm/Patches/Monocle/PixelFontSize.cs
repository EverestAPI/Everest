#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Monocle {
    class patch_PixelFontSize : PixelFontSize {

        public extern Vector2 orig_Measure(string text);
        public new Vector2 Measure(string text) {
            text = Emoji.Apply(text);
            return orig_Measure(text);
        }

        public extern void orig_Draw(char character, Vector2 position, Vector2 justify, Vector2 scale, Color color);
        public new void Draw(char character, Vector2 position, Vector2 justify, Vector2 scale, Color color) {
            if (Emoji.Start <= character &&
                character <= Emoji.Last &&
                !Emoji.IsMonochrome(character)) {
                color = new Color(color.A, color.A, color.A, color.A);
            }
            orig_Draw(character, position, justify, scale, color);
        }

        public extern void orig_Draw(string text, Vector2 position, Vector2 justify, Vector2 scale, Color color, float edgeDepth, Color edgeColor, float stroke, Color strokeColor);
        public new void Draw(string text, Vector2 position, Vector2 justify, Vector2 scale, Color color, float edgeDepth, Color edgeColor, float stroke, Color strokeColor) {
            text = Emoji.Apply(text);

            if (string.IsNullOrEmpty(text))
                return;

            Vector2 offset = Vector2.Zero;
            Vector2 justifyOffs = new Vector2(
                ((justify.X != 0f) ? WidthToNextLine(text, 0) : 0f) * justify.X,
                HeightOf(text) * justify.Y
            );

            for (int i = 0; i < text.Length; i++) {
                if (text[i] == '\n') {
                    offset.X = 0f;
                    offset.Y += LineHeight;
                    if (justify.X != 0f)
                        justifyOffs.X = WidthToNextLine(text, i + 1) * justify.X;
                    continue;
                }

                PixelFontCharacter c = null;
                if (!Characters.TryGetValue(text[i], out c))
                    continue;

                Vector2 pos = position + (offset + new Vector2(c.XOffset, c.YOffset) - justifyOffs) * scale;
                if (stroke > 0f && !Outline) {
                    if (edgeDepth > 0f) {
                        c.Texture.Draw(pos + new Vector2(0f, -stroke), Vector2.Zero, strokeColor, scale);
                        for (float num2 = -stroke; num2 < edgeDepth + stroke; num2 += stroke) {
                            c.Texture.Draw(pos + new Vector2(-stroke, num2), Vector2.Zero, strokeColor, scale);
                            c.Texture.Draw(pos + new Vector2(stroke, num2), Vector2.Zero, strokeColor, scale);
                        }
                        c.Texture.Draw(pos + new Vector2(-stroke, edgeDepth + stroke), Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(0f, edgeDepth + stroke), Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(stroke, edgeDepth + stroke), Vector2.Zero, strokeColor, scale);
                    } else {
                        c.Texture.Draw(pos + new Vector2(-1f, -1f) * stroke, Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(0f, -1f) * stroke, Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(1f, -1f) * stroke, Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(-1f, 0f) * stroke, Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(1f, 0f) * stroke, Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(-1f, 1f) * stroke, Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(0f, 1f) * stroke, Vector2.Zero, strokeColor, scale);
                        c.Texture.Draw(pos + new Vector2(1f, 1f) * stroke, Vector2.Zero, strokeColor, scale);
                    }
                }

                if (edgeDepth > 0f)
                    c.Texture.Draw(pos + Vector2.UnitY * edgeDepth, Vector2.Zero, edgeColor, scale);

                Color cColor = color;
                if (Emoji.Start <= c.Character &&
                    c.Character <= Emoji.Last &&
                    !Emoji.IsMonochrome((char) c.Character)) {
                    cColor = new Color(color.A, color.A, color.A, color.A);
                }
                c.Texture.Draw(pos, Vector2.Zero, cColor, scale);

                offset.X += c.XAdvance;

                int kerning;
                if (i < text.Length - 1 && c.Kerning.TryGetValue(text[i + 1], out kerning)) {
                    offset.X += kerning;
                }
            }
        }

    }
}
