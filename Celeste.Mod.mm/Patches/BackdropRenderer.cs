using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_BackdropRenderer : BackdropRenderer {
        private bool usingSpritebatch;
        private bool usingLoopingSpritebatch;
        
        /// <summary>
        /// Start a new spritebatch for backdrop rendering that uses SamplerState.PointWrap, but is otherwise identical to the one started by StartSpritebatch.
        /// </summary>
        /// <param name="blendState">the blend state for the new spritebatch</param>
        public void StartSpritebatchLooping(BlendState blendState) {
            if (!usingSpritebatch) {
                Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, blendState, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, Matrix);
            }
            usingSpritebatch = true;
            usingLoopingSpritebatch = true;
        }

        [MonoModReplace]
        public new void EndSpritebatch() {
            if (usingSpritebatch)
            {
                Draw.SpriteBatch.End();
            }
            usingSpritebatch = false;
            usingLoopingSpritebatch = false;
        }

        [MonoModReplace]
        public override void Render(Scene scene) {
            BlendState blendState = BlendState.AlphaBlend;
            foreach (Backdrop backdrop in Backdrops)
            {
                if (backdrop.Visible)
                {
                    if (backdrop is Parallax parallax && (!usingLoopingSpritebatch || parallax.BlendState != blendState))
                    {
                        EndSpritebatch();
                        blendState = parallax.BlendState;
                    }
                    if (backdrop is not Parallax && backdrop.UseSpritebatch && usingLoopingSpritebatch) { // make sure non-Parallax backdrops are drawn with the normal spritebatch parameters
                        EndSpritebatch();
                    }
                    if (backdrop.UseSpritebatch && !usingSpritebatch)
                    {
                        if (backdrop is Parallax)
                        {
                            StartSpritebatchLooping(blendState);
                        }
                        else
                        {
                            StartSpritebatch(blendState);
                        }
                    }
                    if (!backdrop.UseSpritebatch && usingSpritebatch)
                    {
                        EndSpritebatch();
                    }
                    backdrop.Render(scene);
                }
            }
            if (Fade > 0f)
            {
                Draw.Rect(-10f, -10f, 340f, 200f, FadeColor * Fade);
            }
            EndSpritebatch();
        }
    }
}