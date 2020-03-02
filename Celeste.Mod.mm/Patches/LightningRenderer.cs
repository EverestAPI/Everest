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
    class patch_LightningRenderer : LightningRenderer {

        private Rectangle levelTileBounds;

        private extern bool orig_Inside(int tx, int ty);
        private bool Inside(int tx, int ty) {
            if (tx < levelTileBounds.Left || ty < levelTileBounds.Top ||
                levelTileBounds.Right <= tx || levelTileBounds.Bottom <= ty)
                return false;

            return orig_Inside(tx, ty);
        }

    }
}
