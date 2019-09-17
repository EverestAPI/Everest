#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Celeste {
    static class patch_Dialog {

        private static Language FallbackLanguage;

        public static extern void orig_Load();
        public static void Load() {
            orig_Load();

            // Remove all empty dummy languages.
            HashSet<string> dummies = new HashSet<string>();
            foreach (Language lang in Dialog.Languages.Values)
                if (lang.Dialog.Count == 0 || string.IsNullOrEmpty(lang.Label))
                    dummies.Add(lang.Id);
            foreach (string id in dummies)
                Dialog.Languages.Remove(id);
        }

        [MonoModReplace]
        public static Language LoadLanguage(string filename) {
            Language language;

            // if (File.Exists(filename + ".export"))
            //     language = Language.FromExport(filename + ".export");
            // else
                language = Language.FromTxt(filename);

            patch_Language.LoadingLanguage = null;

            if (language == null)
                return null;

            Dialog.Languages[language.Id] = language;

            language?.Dialog.Remove("EVEREST_SPLIT_BETWEEN_FILES");
            language?.Cleaned.Remove("EVEREST_SPLIT_BETWEEN_FILES");

            if (language?.Id.Equals("english", StringComparison.InvariantCultureIgnoreCase) ?? false)
                FallbackLanguage = language;

            return language;
        }

        [MonoModReplace]
        public static bool Has(string name, Language language = null) {
            name = name.DialogKeyify();
            if (language == null)
                language = Dialog.Language;

            if (language.Dialog.ContainsKey(name))
                return true;

            if (language != FallbackLanguage)
                return Has(name, FallbackLanguage);

            return false;
        }

        [MonoModReplace]
        public static string Get(string name, Language language = null) {
            name = name.DialogKeyify();
            if (language == null)
                language = Dialog.Language;

            string result;
            if (language.Dialog.TryGetValue(name, out result))
                return result;

            if (language != FallbackLanguage)
                return Get(name, FallbackLanguage);

            return "[" + name + "]";
        }

        [MonoModReplace]
        public static string Clean(string name, Language language = null) {
            name = name.DialogKeyify();
            if (language == null)
                language = Dialog.Language;

            string result;
            if (language.Cleaned.TryGetValue(name, out result))
                return result;

            if (language != FallbackLanguage)
                return Clean(name, FallbackLanguage);

            return "{" + name + "}";
        }

        public static string CleanLevelSet(string name) {
            if (string.IsNullOrEmpty(name)) {
                return Dialog.Clean("levelset_");
            }
            return ("levelset_" + name).DialogCleanOrNull() ?? name.DialogCleanOrNull() ?? name.SpacedPascalCase();
        }

    }
    public static class DialogExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Same as Dialog.Clean, but for level set names.
        /// Tries to find a value under both "LEVELSET_NAME" and "NAME", otherwise returns name.SpacedPascalCase()
        /// </summary>
        public static string CleanLevelSet(string name)
            => patch_Dialog.CleanLevelSet(name);

    }
}
