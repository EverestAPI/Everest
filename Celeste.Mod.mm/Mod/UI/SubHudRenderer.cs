using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    public class SubHudRenderer : HiresRenderer {

        public static bool Cleared;

        public override void BeforeRender(Scene scene) {
            base.BeforeRender(scene);
            Cleared = true;
        }

        public override void RenderContent(Scene scene) {
            if (!scene.Entities.HasVisibleEntities(TagsExt.SubHUD))
                return;
            BeginRender(null, null);
            scene.Entities.RenderOnly(TagsExt.SubHUD);
            EndRender();
        }

        public override void Render(Scene scene) {
            if (DrawToBuffer)
                return; // Drawing to the HUD buffer.
            base.Render(scene);
        }

    }
}
