#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    class patch_Autotiler : Autotiler {

        public string Filename;

        public patch_Autotiler(string filename)
            : base(filename) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // Patching constructors is ugly.
        public extern void orig_ctor_Autotiler(string filename);
        [MonoModConstructor]
        public void ctor_Autotiler(string filename) {
            Filename = filename;
            orig_ctor_Autotiler(filename);
            Everest.Content.Process(filename, this);
        }

    }
    public static class AutotilerExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static string GetFilename(this Autotiler self)
            => ((patch_Autotiler) self).Filename;

    }
}
