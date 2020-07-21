#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;
using System;
using System.Runtime.InteropServices;

namespace FMOD.Studio {
    class patch_EventDescription : EventDescription {

        public patch_EventDescription(IntPtr raw)
            : base(raw) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIfFlag("FMODStub")]
        private static RESULT FMOD_Studio_EventDescription_GetPath(IntPtr eventdescription, [Out] byte[] path, int size, out int retrieved) {
            path[0] = 0;
            retrieved = 1;
            return RESULT.OK;
        }

    }
}
