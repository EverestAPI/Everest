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
    static class patch_SFX {

        private static Dictionary<string, string> byHandle;

        [MonoModReplace]
        public static string EventnameByHandle(string handle) {
            string result;
            if (byHandle.TryGetValue(handle, out result))
                return result;

            if (!Everest.Flags.IsDisabled) {
                /* Vanilla chapter 3 has got an invalid ambience sound
                 * handle in the reception room (introducing Oshiro).
                 * It ends up being silent in vanilla Celeste.
                 */
                if (handle == "env_amb_03_interior_main")
                    return EventnameByHandle("env_amb_03_interior");
            }

            return handle;
        }

    }
}
