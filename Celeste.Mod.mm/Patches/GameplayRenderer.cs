using Monocle;
using MonoMod;
using System;

namespace Celeste {
    class patch_GameplayRenderer : GameplayRenderer {

        public static bool RenderDebug;

        [MonoModReplace]
        public override void Render(Scene scene) {
            Begin();
            scene.Entities.RenderExcept(Tags.HUD | TagsExt.SubHUD);
            if (RenderDebug || Engine.Commands.Open) {
                scene.Entities.DebugRender(Camera);
            }
            End();
        }

    }
    public static class GameplayRendererExt {

        [Obsolete("Use GameplayRenderer.RenderDebug instead.")]
        public static bool RenderDebug {
            get {
                return patch_GameplayRenderer.RenderDebug;
            }
            set {
                patch_GameplayRenderer.RenderDebug = value;
            }
        }

    }
}
