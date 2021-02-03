#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

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

            /* Vanilla chapter 3 has got an invalid ambience sound
             * handle in the reception room (introducing Oshiro) in the maps .bin file.
             * It ends up being silent in vanilla Celeste.
             */
            if (handle == "env_amb_03_interior_main")
                return EventnameByHandle("env_amb_03_interior");

            return handle;
        }

    }
}
