#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using FMOD;
using FMOD.Studio;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Celeste {
    static class patch_Sfxs {

        private static Dictionary<string, string> byHandle;

        [MonoModReplace]
        public static string EventnameByHandle(string handle) {
            string result;
            if (byHandle.TryGetValue(handle, out result))
                return result;
            return handle;
        }

    }
}
