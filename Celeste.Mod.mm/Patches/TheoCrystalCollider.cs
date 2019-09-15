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
    // TheoCrystalCollider became HoldableCollider in Celeste 1.2.6.X
    // Holdable might be missing from versions of Celeste pre-1.2.5.X

    [MonoModLinkTo("Celeste.HoldableCollider")]
    class TheoCrystalCollider {
    }

    class HoldableCollider {
        [MonoModLinkTo("Celeste.HoldableCollider", "System.Void .ctor(System.Action`1<Celeste.Holdable>,Monocle.Collider)")]
        [MonoModForceCall]
        [MonoModIgnore]
        public extern void ctor(Action<Holdable> onCollide, Collider collider = null);
        [MonoModConstructor]
        public void ctor(Action<TheoCrystal> onCollide, Collider collider = null) {
            ctor(onCollide == null ? null : new Action<Holdable>(h => onCollide(h.Entity as TheoCrystal)), collider);
        }
    }
}
