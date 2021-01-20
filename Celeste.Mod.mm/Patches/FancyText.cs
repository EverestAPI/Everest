#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste {
    // The FancyText ctor is private.
    class patch_FancyText {

        private extern void orig_AddWord(string word);
        private void AddWord(string word) {
            word = Emoji.Apply(word);
            orig_AddWord(word);
        }

        public class patch_Char : FancyText.Char {
            public extern void orig_Draw(PixelFont font, float baseSize, Vector2 position, Vector2 scale, float alpha);
            public new void Draw(PixelFont font, float baseSize, Vector2 position, Vector2 scale, float alpha) {
                Color prevColor = Color;

                if (Emoji.Start <= Character &&
                    Character <= Emoji.Last &&
                    !Emoji.IsMonochrome((char) Character)) {
                    Color = new Color(Color.A, Color.A, Color.A, Color.A);
                }

                orig_Draw(font, baseSize, position, scale, alpha);

                Color = prevColor;
            }
        }

    }
}
