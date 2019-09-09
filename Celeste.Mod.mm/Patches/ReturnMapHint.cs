#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_ReturnMapHint : ReturnMapHint {

        [MonoModReplace]
        public static new string GetCheckpointPreviewName(AreaKey area, string level) {
            return patch_OuiChapterPanel._GetCheckpointPreviewName(area, level);
        }

    }
}
