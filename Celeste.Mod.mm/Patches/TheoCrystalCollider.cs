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

    [MonoModIfFlag("V2:TheoCrystalCollider")]
    [MonoModPatch("Celeste.TheoCrystalCollider")]
    [MonoModLinkTo("Celeste.HoldableCollider")]
    class link_TheoCrystalCollider {
    }

    [MonoModIfFlag("V1:TheoCrystalCollider")]
    [MonoModPatch("Celeste.TheoCrystalCollider")]
    class shim_TheoCrystalCollider {
        [MonoModLinkTo("Celeste.TheoCrystalCollider", "System.Void .ctor(System.Action`1<Celeste.TheoCrystal>,Monocle.Collider)")]
        [MonoModForceCall]
        [MonoModIgnore]
        public extern void ctor_old(Action<TheoCrystal> onCollide, Collider collider = null);
        [MonoModConstructor]
        public void ctor_new(Action<Holdable> onCollide, Collider collider = null) {
            ctor_old(onCollide == null ? null : new Action<TheoCrystal>(h => onCollide(h.Get<Holdable>())), collider);
        }
    }

    [MonoModIfFlag("V1:TheoCrystalCollider")]
    [MonoModPatch("Celeste.HoldableCollider")]
    [MonoModLinkTo("Celeste.TheoCrystalCollider")]
    class link_HoldableCollider {
    }

    [MonoModIfFlag("V2:TheoCrystalCollider")]
    [MonoModPatch("Celeste.HoldableCollider")]
    class shim_HoldableCollider {
        [MonoModLinkTo("Celeste.HoldableCollider", "System.Void .ctor(System.Action`1<Celeste.Holdable>,Monocle.Collider)")]
        [MonoModForceCall]
        [MonoModIgnore]
        public extern void ctor_new(Action<Holdable> onCollide, Collider collider = null);
        [MonoModConstructor]
        public void ctor_old(Action<TheoCrystal> onCollide, Collider collider = null) {
            ctor_new(onCollide == null ? null : new Action<Holdable>(h => onCollide(h.Entity as TheoCrystal)), collider);
        }
    }
}
