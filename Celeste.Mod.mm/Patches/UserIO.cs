#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
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
        // Old:
        public static T Load<T>(string path) where T : class
        // New:
        public static T Load<T>(string path, bool backup = false) where T : class
        */

        // V2 is present, fill V1 for old mods.
        [MonoModIfFlag("V2:UserIOLoad")]
        public static T Load<T>(string path) where T : class
            => Load<T>(path, false);

        // V1 is present, fill V2 for new mods.
        [MonoModIfFlag("V1:UserIOLoad")]
        public static T Load<T>(string path, bool backup = false) where T : class
            => Load<T>(path);

    }
}
