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
using Microsoft.Xna.Framework;

namespace Celeste {
    class patch_Holdable : Holdable {

        [MonoModLinkTo("Celeste.Holdable", ".ctor")]
        [MonoModForceCall]
        [MonoModRemove]
        public extern void ctor(float cannotHoldDelay = 0.1f);
        [MonoModConstructor]
        public void ctor() {
            ctor(0.1f);
        }

    }
}
