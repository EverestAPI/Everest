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
        public PlayerSprite Sprite => sprite;
        private List<MTexture> bangs;
        private float wave;
        public float Wave => wave;

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
                hair.Draw(Nodes[i], origin, GetHairColor(i), GetHairScale(i));
            }
        }

        [MonoModIgnore]
        private extern Vector2 GetHairScale(int index);
        public Vector2 PublicGetHairScale(int index) => GetHairScale(index);

        public Color GetHairColor(int index) {
            return Color * Alpha;
        }

    }
    public static class PlayerHairExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static Vector2 GetHairScale(this PlayerHair self, int index)
            => ((patch_PlayerHair) self).PublicGetHairScale(index);

        public static Color GetHairColor(this PlayerHair self, int index)
            => ((patch_PlayerHair) self).GetHairColor(index);

        public static PlayerSprite GetSprite(this PlayerHair self)
            => ((patch_PlayerHair) self).Sprite;

        public static float GetWave(this PlayerHair self)
            => ((patch_PlayerHair) self).Wave;

    }
}
