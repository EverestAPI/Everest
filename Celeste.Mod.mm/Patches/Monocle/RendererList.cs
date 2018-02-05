#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Monocle {
    class patch_RendererList {

        // We're effectively in RendererList, but still need to "expose" private fields to our mod.

        [MonoModIgnore]
        internal extern void UpdateLists();
        public void _UpdateLists()
            => UpdateLists();

    }
    public static class RendererListExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static void UpdateLists(this RendererList self)
            => ((patch_RendererList) (object) self)._UpdateLists();

    }
}
