using MonoMod;

namespace Monocle {
    public abstract class patch_VirtualInput : VirtualInput {
#pragma warning disable CS0626 // method is external and has no attribute
        public extern void orig_ctor();
        public extern void orig_Deregister();
#pragma warning restore CS0626

        // apply patches to lock VirtualInputs, so that we don't modify it while MInput.UpdateVirtualInputs() is iterating it.

        [MonoModConstructor]
        public void ctor() {
            lock (patch_MInput.VirtualInputs) {
                orig_ctor();
            }
        }

        public new void Deregister() {
            lock (patch_MInput.VirtualInputs) {
                orig_Deregister();
            }
        }
    }
}
