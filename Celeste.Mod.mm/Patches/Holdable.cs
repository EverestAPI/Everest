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
