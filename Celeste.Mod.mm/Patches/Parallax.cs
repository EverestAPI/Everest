#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;

namespace Celeste {
    class patch_Parallax : Parallax {

        private float fadeIn;
        
        public patch_Parallax(MTexture texture) : base(texture) {
            // no-op, ignored by MonoMod
        }

        /// <summary>
        /// An optimized version of the vanilla Render method. Only works if the SamplerState is set to PointWrap.
        /// </summary>
        /// <param name="scene">The Level to render the Parallax to.</param>
        public void ImprovedRender(Scene scene) {
            if (((patch_MTexture) Texture).IsPacked) {
                Render(scene);
                return;
            }
            Vector2 camera = ((scene as Level).Camera.Position + CameraOffset).Floor();
            Vector2 position = (Position - camera * Scroll).Floor();
            float alpha = fadeIn * Alpha * FadeAlphaMultiplier;
            if (FadeX != null) {
                alpha *= FadeX.Value(camera.X + 160f);
            }
            if (FadeY != null) {
                alpha *= FadeY.Value(camera.Y + 90f);
            }
            Color color = Color;
            if (alpha < 1f) {
                color *= alpha;
            }
            if (color.A <= 1) {
                return;
            }
            if (LoopX) {
                position.X = (position.X % Texture.Width - Texture.Width) % Texture.Width;
            }
            if (LoopY) {
                position.Y = (position.Y % Texture.Height - Texture.Height) % Texture.Height;
            }
            SpriteEffects flip = SpriteEffects.None;
            if (FlipX) {
                flip |= SpriteEffects.FlipHorizontally;
            }
            if (FlipY) {
                flip |= SpriteEffects.FlipVertically;
            }
            Rectangle rect = new Rectangle(0, 0,
                                           LoopX ? (int) Math.Ceiling(Celeste.GameWidth - position.X) : Texture.Width,
                                           LoopY ? (int) Math.Ceiling(Celeste.GameHeight - position.Y) : Texture.Height);
            ((patch_MTexture) Texture).Draw(position, Vector2.Zero, color, 1f, 0f, flip, rect);
        }
    }
}