#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
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

namespace Celeste {
    class patch_PlayerHair : PlayerHair {

        // We're effectively in PlayerHair, but still need to "expose" private fields to our mod.
        private PlayerSprite sprite;
        private List<MTexture> bangs;
        private float wave;

        public patch_PlayerHair(PlayerSprite sprite)
            : base(sprite) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModReplace]
        public override void Render() {
            if (!sprite.HasHair)
                return;

            Vector2 origin = new Vector2(5f, 5f);
            Color colorBorder = Border * Alpha;
            Color colorHair = Color * Alpha;

            if (DrawPlayerSpriteOutline) {
                Color colorSprite = sprite.Color;
                Vector2 position = sprite.Position;
                sprite.Color = colorBorder;
                sprite.Position = position + new Vector2(0f, -1f);
                sprite.Render();
                sprite.Position = position + new Vector2(0f, 1f);
                sprite.Render();
                sprite.Position = position + new Vector2(-1f, 0f);
                sprite.Render();
                sprite.Position = position + new Vector2(1f, 0f);
                sprite.Render();
                sprite.Color = colorSprite;
                sprite.Position = position;
            }

            Nodes[0] = Nodes[0].Floor();
            if (colorBorder.A > 0) {
                for (int i = 0; i < sprite.HairCount; i++) {
                    int hairFrame = sprite.HairFrame;
                    MTexture hair = (i == 0) ? bangs[hairFrame] : GFX.Game["characters/player/hair00"];
                    Vector2 hairScale = GetHairScale(i);
                    hair.Draw(Nodes[i] + new Vector2(-1f, 0f), origin, colorBorder, hairScale);
                    hair.Draw(Nodes[i] + new Vector2(1f, 0f), origin, colorBorder, hairScale);
                    hair.Draw(Nodes[i] + new Vector2(0f, -1f), origin, colorBorder, hairScale);
                    hair.Draw(Nodes[i] + new Vector2(0f, 1f), origin, colorBorder, hairScale);
                }
            }

            for (int i = sprite.HairCount - 1; i >= 0; i--) {
                int hairFrame = sprite.HairFrame;
                MTexture hair = (i == 0) ? bangs[hairFrame] : GFX.Game["characters/player/hair00"];
                hair.Draw(Nodes[i], origin, GetHairColor(i, colorHair), GetHairScale(i));
            }
        }

        [MonoModIgnore]
        private extern Vector2 GetHairScale(int index);

        public Color GetHairColor(int index, Color colorHair) {
            // TODO: MOVE THIS OUT OF HERE.
            if (!CoreModule.Instance.Settings.RainbowMode)
                return colorHair;
            Color colorRainbow = ColorFromHSV((index / (float) sprite.HairCount) * 180f + wave * 60f, 0.6f, 0.6f);
            return new Color(
                (colorHair.R / 255f) * 0.3f + (colorRainbow.R / 255f) * 0.7f,
                (colorHair.G / 255f) * 0.3f + (colorRainbow.G / 255f) * 0.7f,
                (colorHair.B / 255f) * 0.3f + (colorRainbow.B / 255f) * 0.7f,
                colorHair.A
            );
        }

        // Algorithms found randomly on the net - best source for HSV <-> RGB conversion ever:tm:

        private static void ColorToHSV(Color c, out float h, out float s, out float v) {
            float r = c.R / 255f;
            float g = c.G / 255f;
            float b = c.B / 255f;
            float min, max, delta;
            min = Math.Min(Math.Min(r, g), b);
            max = Math.Max(Math.Max(r, g), b);
            v = max;
            delta = max - min;
            if (max != 0) {
                s = delta / max;

                if (r == max)
                    h = (g - b) / delta;
                else if (g == max)
                    h = 2 + (b - r) / delta;
                else
                    h = 4 + (r - g) / delta;
                h *= 60f;
                if (h < 0)
                    h += 360f;
            } else {
                s = 0f;
                h = 0f;
            }
        }

        private static Color ColorFromHSV(float hue, float saturation, float value) {
            int hi = (int) (Math.Floor(hue / 60f)) % 6;
            float f = hue / 60f - (float) Math.Floor(hue / 60f);

            value = value * 255;
            int v = (int) (value);
            int p = (int) (value * (1 - saturation));
            int q = (int) (value * (1 - f * saturation));
            int t = (int) (value * (1 - (1 - f) * saturation));

            if (hi == 0)
                return new Color(255, v, t, p);
            else if (hi == 1)
                return new Color(255, q, v, p);
            else if (hi == 2)
                return new Color(255, p, v, t);
            else if (hi == 3)
                return new Color(255, p, q, v);
            else if (hi == 4)
                return new Color(255, t, p, v);
            else
                return new Color(255, v, p, q);
        }

    }
}
