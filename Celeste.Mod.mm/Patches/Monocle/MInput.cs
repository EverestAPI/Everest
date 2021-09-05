#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using System.Collections.Generic;

namespace Monocle {
    class patch_MInput {
        // vanilla internal field
        internal static List<VirtualInput> VirtualInputs;

        // lock VirtualInputs so that the VirtualInput class doesn't modify it while UpdateVirtualInputs() iterates it.
        private static extern void orig_UpdateVirtualInputs();
        private static void UpdateVirtualInputs() {
            lock (VirtualInputs) {
                orig_UpdateVirtualInputs();
            }
        }
    }
}