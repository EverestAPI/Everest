#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_Skybox : Skybox {

        public patch_Skybox(VirtualTexture texture, float size = 25) 
            : base(texture, size) {
        }

        public extern void orig_Draw(Matrix matrix, Color color);

        public void Draw(Matrix matrix, Color color) {
            // the orig method doesn't reset DepthStencilState to None.
            // Some mods may begin a new SpriteBatch with DepthStencilState set to Default,
            // which causes the Skybox to be rendered in front of the mountain.
            Engine.Graphics.GraphicsDevice.DepthStencilState = DepthStencilState.None;
            orig_Draw(matrix, color);
        }
    }
}
