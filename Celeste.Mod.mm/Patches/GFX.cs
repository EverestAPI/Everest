#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Celeste {
    static class patch_GFX {

        public static extern void orig_Load();
        public static void Load() {
            orig_Load();

            if (GFX.Game != null) {
                // Celeste 1.3.0.0 gets rid of those.
                for (int i = 0; i <= 29; i++)
                    GFX.Game[string.Format("objects/checkpoint/flag{0:D2}", i)] = GFX.Game["util/pixel"];
                for (int i = 0; i <= 27; i++)
                    GFX.Game[string.Format("objects/checkpoint/obelisk{0:D2}", i)] = GFX.Game["util/pixel"];

                GFX.Gui["fileselect/assist"] = GFX.Game["util/pixel"];
                GFX.Gui["fileselect/cheatmode"] = GFX.Game["util/pixel"];
            }
        }

    }
}
