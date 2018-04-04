#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

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
