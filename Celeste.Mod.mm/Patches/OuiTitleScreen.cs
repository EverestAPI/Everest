#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_OuiTitleScreen : OuiTitleScreen {

        // We're effectively in OuiTitleScreen, but still need to "expose" private fields to our mod.
        private string version;

        // Patching constructors is ugly.
        public extern void orig_ctor_OuiTitleScreen();
        [MonoModConstructor]
        public void ctor_OuiTitleScreen() {
            orig_ctor_OuiTitleScreen();

            version += $"\nEverest v.{Everest.VersionString}";
        }

    }
}
