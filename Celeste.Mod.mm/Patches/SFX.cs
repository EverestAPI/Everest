#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using MonoMod;
using System.Collections.Generic;

namespace Celeste {
    static class patch_SFX {

        private static Dictionary<string, string> byHandle;

        [MonoModReplace]
        public static string EventnameByHandle(string handle) {
            if (byHandle.TryGetValue(handle, out string result))
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
