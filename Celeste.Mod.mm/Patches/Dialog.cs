#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    static class patch_Dialog {

        public static extern void orig_InitLanguages();
        public static void InitLanguages() {
            // TODO: Apply custom texts.
            orig_InitLanguages();
            Everest.Events.Dialog.InitLanguages();
        }

        public static string Get(string name, Language language = null) {
            if (language == null)
                language = Dialog.Language;

            string result;
            if (language.Dialog.TryGetValue(name, out result))
                return result;

            return "[" + name + "]";
        }

        public static string Clean(string name, Language language = null) {
            if (language == null)
                language = Dialog.Language;

            string result;
            if (language.Cleaned.TryGetValue(name, out result))
                return result;

            return "{" + name + "}";
        }

    }
}
