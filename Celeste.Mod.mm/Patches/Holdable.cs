#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;

namespace Celeste {
    class patch_Holdable : Holdable {

        [MonoModLinkTo("Celeste.Holdable", "System.Void .ctor(System.Single)")]
        [MonoModForceCall]
        [MonoModRemove]
        public extern void ctor(float cannotHoldDelay = 0.1f);
        [MonoModConstructor]
        public void ctor() {
            ctor(0.1f);
        }

    }
}
