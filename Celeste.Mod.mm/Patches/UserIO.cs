#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Celeste {
    static class patch_UserIO {

        [MonoModIgnore]
        public static bool Saving { get; private set; }

        private static extern string orig_GetSavePath(string dir);
        private static string GetSavePath(string dir) {
            string env = Environment.GetEnvironmentVariable("EVEREST_SAVEPATH");
            if (!string.IsNullOrEmpty(env))
                return Path.Combine(env, dir);

            try {
                return orig_GetSavePath(dir);
            } catch (NotSupportedException) {
                return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), dir);
            }
        }

        /*
        // V1:
        public static IEnumerator SaveHandler(bool file, bool settings)
        // V2:
        public static void SaveHandler(bool file, bool settings)
        public static IEnumerator SaveRoutine(bool file, bool settings)
        */

        // The new SaveRoutine doesn't need to be patched.
        [MonoModIgnore]
	[MonoModPublic]
        public static extern IEnumerator SaveRoutine(bool file, bool settings);

        // V2 is present, provide V1 for old mods.
        [MonoModIfFlag("V2:UserIOLoad")]
        [MonoModLinkFrom("System.Collections.IEnumerator Celeste.UserIO::SaveHandler(System.Boolean,System.Boolean)")]
        public static IEnumerator SaveHandlerForOldMods(bool file, bool settings) {
            if (Saving)
                return SaveNonHandler();
            Saving = true;
            // Note how we're calling SaveRoutine, not orig_SaveHandler.
            return new SafeRoutine(SaveRoutine(file, settings));
        }

        // V1 is present, relink from V2 for new mods and fix V1.
        public static extern IEnumerator orig_SaveHandler(bool file, bool settings);
        [MonoModIfFlag("V1:UserIOLoad")]
        [MonoModLinkFrom("System.Collections.IEnumerator Celeste.UserIO::SaveRoutine(System.Boolean,System.Boolean)")]
        public static IEnumerator SaveHandler(bool file, bool settings) {
            if (Saving)
                return SaveNonHandler();
            Saving = true; // Originally set in the coroutine, which is too late in case it gets added twice.
            // Wrap the original SaveHandler in a SafeRoutine helper.
            // This is needed because the entity holding the routine could be removed,
            // leaving this in a "hanging" state.
            return new SafeRoutine(orig_SaveHandler(file, settings));
        }

        private static IEnumerator SaveNonHandler() {
            yield break;
        }

        /*
        // V1:
        public static T Load<T>(string path) where T : class
        // V2:
        public static T Load<T>(string path, bool backup = false) where T : class
        */

        // V2 is present, provide V1 for old mods.
        [MonoModIfFlag("V2:UserIOLoad")]
        public static T Load<T>(string path) where T : class
            => Load<T>(path, false);

        // V1 is present, provide V2 for new mods.
        [MonoModIfFlag("V1:UserIOLoad")]
        public static T Load<T>(string path, bool backup = false) where T : class
            => Load<T>(path);

    }
}
