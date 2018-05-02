#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

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
