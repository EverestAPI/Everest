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

        public override void RenderContent(Scene scene) {
            if (!scene.Entities.HasVisibleEntities(TagsExt.SubHUD))
                return;
            BeginRender(null, null);
            scene.Entities.RenderOnly(TagsExt.SubHUD);
            EndRender();
        }

    }
}
