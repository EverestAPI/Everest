using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

using _SubHudRenderer = Celeste.Mod.UI.SubHudRenderer;

namespace Celeste.Mod.UI {
    // Don't inherit from HiresRenderer, as we need our own buffer.
    public class SubHudRenderer : Renderer {

        public static VirtualRenderTarget Buffer;

        public static bool DrawToBuffer {
            get {
                return Buffer != null && (Engine.ViewWidth < 1920 || Engine.ViewHeight < 1080);
            }
        }

        public static void BeginRender(BlendState blend = null, SamplerState sampler = null) {
            Draw.SpriteBatch.Begin(
                SpriteSortMode.Deferred,
                blend ?? BlendState.AlphaBlend,
                sampler ?? SamplerState.LinearClamp,
                DepthStencilState.Default,
                RasterizerState.CullNone,
                null,
                DrawToBuffer ? Matrix.Identity : Engine.ScreenMatrix
            );
        }

        public static void EndRender() {
            Draw.SpriteBatch.End();
        }

        public override void BeforeRender(Scene scene) {
            Everest.Events.SubHudRenderer.BeforeRender(this, scene);
            if (!DrawToBuffer)
                return;

            Engine.Graphics.GraphicsDevice.SetRenderTarget(Buffer);
            Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
            RenderContent(scene);
        }

        public override void Render(Scene scene) {
            if (!DrawToBuffer) {
                RenderContent(scene);
                return;
            }

            Draw.SpriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.Default,
                RasterizerState.CullNone,
                null,
                Engine.ScreenMatrix
            );
            Draw.SpriteBatch.Draw(Buffer, new Vector2(-1f, -1f), Color.White);
            Draw.SpriteBatch.End();
        }

        public void RenderContent(Scene scene) {
            if (!scene.Entities.HasVisibleEntities(TagsExt.SubHUD))
                return;

            BeginRender(null, null);
            scene.Entities.RenderOnly(TagsExt.SubHUD);
            EndRender();
        }

    }
}

namespace Celeste.Mod {
    public static partial class Everest {
        public static partial class Events {
            public static class SubHudRenderer {
                public delegate void BeforeRenderHandler(_SubHudRenderer renderer, Scene scene);
                /// <summary>
                /// Called at the beginning of <see cref="_SubHudRenderer.BeforeRender"/>.
                /// Note that this happens before clearing and drawing to the buffer, if applicable.
                /// </summary>
                public static event BeforeRenderHandler OnBeforeRender;
                internal static void BeforeRender(_SubHudRenderer renderer, Scene scene)
                    => OnBeforeRender?.Invoke(renderer, scene);
            }
        }
    }
}
