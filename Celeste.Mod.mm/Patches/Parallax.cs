#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Reflection;

namespace Celeste {
    class patch_Parallax : Parallax {

        private float fadeIn;
        
        public patch_Parallax(MTexture texture) : base(texture) {
            // no-op, ignored by MonoMod
        }

        public extern void orig_Render(Scene scene);

        // not pretty, but because of SpriteSortMode.Deferred, we can't just check Draw.SpriteBatch.GraphicsDevice.SamplerStates[0], so we need reflection since we can't patch XNA
        private static readonly FieldInfo spriteBatchSamplerState = typeof(SpriteBatch).GetField("samplerState", BindingFlags.Instance | BindingFlags.NonPublic);
        public override void Render(Scene scene) {
            if (((patch_MTexture) Texture).IsPacked // atlas-packed textures do not support wrapping spritebatches and are therefore drawn normally
                || spriteBatchSamplerState.GetValue(Draw.SpriteBatch) != SamplerState.PointWrap) { // if Parallax.Render is called from outside BackdropRenderer.Render, it might use a different SamplerState
                // in either case, fall back to vanilla rendering
                orig_Render(scene);
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
            // use modulo instead of vanilla's loops, which might be very inefficient for a small looping styleground far offscreen
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
            ((patch_MTexture) Texture).Draw(position, Vector2.Zero, color, 1f, 0f, flip, rect); // take advantage of the PointWrap sampler state to draw in a single draw call
        }
    }
}