using Monocle;
using MonoMod;

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

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

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
