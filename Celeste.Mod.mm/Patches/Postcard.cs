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
    class patch_Postcard : Postcard {

        public patch_Postcard(string msg, int area)
            : base(msg, area) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // 1.3.0.0 gets rid of the 1-arg ctor.
        // We're adding a new ctor, thus can't call base (Celeste.Postcard::.ctor) without a small workaround.
        [MonoModLinkTo("Celeste.Postcard", ".ctor")]
        [MonoModForceCall]
        [MonoModRemove]
        public extern void base_ctor(string msg, string sfxEventIn, string sfxEventOut);
        [MonoModConstructor]
        public void ctor(string msg) {
            base_ctor(msg, "event:/ui/main/postcard_csides_in", "event:/ui/main/postcard_csides_out");
        }

    }
}
