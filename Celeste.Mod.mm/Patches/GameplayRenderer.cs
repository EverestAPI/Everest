#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
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
