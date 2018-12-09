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

        private static bool LoadOriginalLanguageFiles;
        private static bool LoadModLanguageFiles;

        public static extern void orig_Load();
        public static void Load() {
            LoadOriginalLanguageFiles = true;
            LoadModLanguageFiles = false;

            orig_Load();
        }

        public static extern Language orig_LoadLanguage(string filename);
        [PatchLoadLanguage] // Manually manipulate the method via MonoModRules
        public static Language LoadLanguage(string filename) {
            Language language = orig_LoadLanguage(filename);

            if (language?.Id.Equals("english", StringComparison.InvariantCultureIgnoreCase) ?? false)
                FallbackLanguage = language;

            return language;
        }

        public static extern void orig_InitLanguages();
        public static void InitLanguages() {
            LoadOriginalLanguageFiles = false;
            LoadModLanguageFiles = true;

            foreach (ModAsset asset in Everest.Content.Map.Values) {
                if (!asset.PathVirtual.StartsWith("Dialog/"))
                    continue;
                LoadLanguage(Path.Combine(Engine.ContentDirectory, "Dialog", asset.PathVirtual.Substring(7) + ".txt"));
            }

            // Remove all empty dummy languages.
            HashSet<string> dummies = new HashSet<string>();
            foreach (Language lang in Dialog.Languages.Values)
                if (lang.Dialog.Count == 0)
                    dummies.Add(lang.Id);
            foreach (string id in dummies)
                Dialog.Languages.Remove(id);

            orig_InitLanguages();
        }

        private static IEnumerable<string> _GetLanguageText(string path, Encoding encoding) {
            if (LoadOriginalLanguageFiles && File.Exists(path))
                foreach (string text in File.ReadLines(path, encoding))
                    yield return text;

            if (!LoadModLanguageFiles)
                yield break;

            path = path.Substring(Everest.Content.PathContentOrig.Length + 1);
            path = path.Replace('\\', '/');
            path = path.Substring(0, path.Length - 4);
            string dummy = $"LANGUAGE={path.Substring(7).ToLowerInvariant()}";
            foreach (ModAsset asset in
                Everest.Content.Mods
                .Select(mod => mod.Map.TryGetValue(path, out ModAsset asset) ? asset : null)
                .Where(asset => asset != null)
            ) {
                // Feed a dummy language line. All empty languages are removed afterwards.
                yield return dummy;
                using (StreamReader reader = new StreamReader(asset.Stream, encoding))
                    while (reader.Peek() != -1)
                        yield return reader.ReadLine().Trim('\r', '\n').Trim();
            }
        }

        private static bool _ContainsKey(Dictionary<string, string> dialog, string key) {
            dialog.Remove(key);
            return false;
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
