#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.UI;
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
    class patch_HudRenderer : HudRenderer {

        public override void BeforeRender(Scene scene) {
            if (!DrawToBuffer)
                return;

            Engine.Graphics.GraphicsDevice.SetRenderTarget(Buffer);

            if (!SubHudRenderer.Cleared) {
                // SubHudRenderer.BeforeRender renders to the same buffer and clears it for us.
                Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
            }
            SubHudRenderer.Cleared = false;

            RenderContent(scene);
        }

    }
}
