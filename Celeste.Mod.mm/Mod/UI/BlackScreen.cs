using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.UI {
    /// <summary>
    /// A Scene that forces the screen to be black.
    /// </summary>
    class BlackScreen : Scene {
        public Action RunAfterRender = null;

        public override void Render() {
            base.Render();
            Engine.Graphics.GraphicsDevice.Clear(Color.Black);
        }

        public override void AfterRender() {
            base.AfterRender();

            if (RunAfterRender != null) {
                RunAfterRender();
                RunAfterRender = null;
            }
        }
    }
}
