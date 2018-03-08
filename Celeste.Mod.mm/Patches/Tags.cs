#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
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
    // Tags is static.
    class patch_Tags {

        public static extern void orig_Initialize();
        public static void Initialize() {
            orig_Initialize();
            TagsExt.SubHUD = new BitTag("subHUD");
            // TODO: Allow mods to register tags easily.
        }

    }
    public static class TagsExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Tag to be used for entities rendering like a HUD, but below the actual game HUD.
        /// </summary>
        public static BitTag SubHUD;

    }
}
