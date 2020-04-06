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
        [PatchDialogLoader]
        public static void Load() {
            orig_Load();
            PostLanguageLoad();
        }

        public static List<string> GetVanillaLanguageFileList(string root, string searchPattern, SearchOption searchOption) {
            return Directory.GetFiles(root, searchPattern, searchOption)
                .Select(f => f.Substring(Everest.Content.PathContentOrig.Length + 1).Replace('\\', '/'))
                .ToList();
        }

        private static string[] _GetFiles(string root, string searchPattern, SearchOption searchOption) {
            // initialize a list of files with vanilla language files
            List<string> allFiles = GetVanillaLanguageFileList(root, searchPattern, searchOption);

            // look up for all mod dialog files
            List<string> modFiles;
            lock (Everest.Content.Map)
                modFiles = Everest.Content.Map.Values
                    .Where(a => a.Type == typeof(AssetTypeDialog) || a.Type == typeof(AssetTypeDialogExport))
                    .Select(a => Path.ChangeExtension(a.PathVirtual, "txt"))
                    .ToList();

            // merge them with the vanilla language files in a case-insensitive manner (if a file is called "english.txt", don't add it)
            foreach (string modFile in modFiles) {
                if (allFiles.All(file => !file.Equals(modFile, StringComparison.InvariantCultureIgnoreCase))) {
                    allFiles.Add(modFile);
                }
            }

            // turn them into absolute paths, then return them.
            return allFiles
                .Select(f => Path.Combine(Everest.Content.PathContentOrig, f.Replace('/', Path.DirectorySeparatorChar)))
                .ToArray();
        }

        public static void PostLanguageLoad() {
            Language[] langs = Dialog.Languages.Values.Distinct().ToArray();
            Dialog.Languages.Clear();

            foreach (Language lang in langs) {
                if (lang.Dialog.Count == 0 || (string.IsNullOrEmpty(lang.Label) && string.IsNullOrEmpty(lang.FontFace) && string.IsNullOrEmpty(lang.IconPath))) {
                    if (lang.Icon != null)
                        lang.Dispose();
                    continue;
                }

                if (string.IsNullOrEmpty(lang.FontFace)) {
                    lang.FontFace = "Renogare";
                    lang.FontFaceSize = 64;
                }

                if (lang.Icon == null) {
                    lang.Icon = new MTexture(VirtualContent.CreateTexture(Path.Combine("Graphics", "Atlases", "Gui", "menu", "langnoicon")));
                }

                Dialog.Languages[lang.Id] = lang;
                if (lang.Id.Equals("english", StringComparison.InvariantCultureIgnoreCase))
                    FallbackLanguage = lang;
            }

            Dialog.OrderedLanguages = new List<Language>();
            foreach (KeyValuePair<string, Language> keyValuePair in Dialog.Languages)
                Dialog.OrderedLanguages.Add(keyValuePair.Value);
            Dialog.OrderedLanguages.Sort((a, b) => a.Order != b.Order ? a.Order - b.Order : a.Id.CompareTo(b.Id));
        }

        public static void RefreshLanguages() {
            PostLanguageLoad();

            Dialog.Language = Dialog.Languages[Dialog.Language.Id];
        }

        public static extern Language orig_LoadLanguage(string filename);
        public static Language LoadLanguage(string filename) {
            patch_Language.LoadingLanguage = null;
            patch_Language.LoadOrigLanguage = true;
            patch_Language.LoadModLanguage = false;
            Language lang = orig_LoadLanguage(filename);
            patch_Language.LoadingLanguage = null;

            Dialog.Languages.Remove(lang.Id);

            string pathExp = filename.Substring(Everest.Content.PathContentOrig.Length + 1).Replace('\\', '/');
            foreach (ModAsset asset in
                Everest.Content.Mods
                .Select(mod => mod.Map.TryGetValue(pathExp, out ModAsset asset) ? asset : null)
                .Where(asset => asset != null && asset.Type == typeof(AssetTypeDialogExport))
            ) {
                lang = MergeLanguages(lang, (patch_Language) patch_Language.FromModExport(asset));
            }

            patch_Language filler = (patch_Language) (patch_Language.LoadingLanguage = new Language());
            filler.LineSources = new Dictionary<string, string>();
            filler.ReadCount = new Dictionary<string, int>();
            if (lang != null) {
                foreach (KeyValuePair<string, string> kvp in lang.Dialog) {
                    filler.Dialog[kvp.Key] = kvp.Value;
                    // filler.ReadCount[kvp.Key] = -1; // Ignores vanilla conflicts.
                }
            }
            patch_Language.LoadOrigLanguage = false;
            patch_Language.LoadModLanguage = true;
            lang = MergeLanguages(lang, (patch_Language) Language.FromTxt(filename));
            patch_Language.LoadingLanguage = null;

            if (lang != null) {
                lang.Dialog.Remove("EVEREST_SPLIT_BETWEEN_FILES");
                lang.Cleaned.Remove("EVEREST_SPLIT_BETWEEN_FILES");

                if (lang.Dialog.Count > 0)
                    Dialog.Languages[lang.Id] = lang;
            }

            return lang;
        }

        private static Language MergeLanguages(Language orig, patch_Language mod) {
            if (orig == null)
                return mod;

            if (string.IsNullOrEmpty(orig.Label) && string.IsNullOrEmpty(orig.IconPath)) {
                orig.Id = mod.Id;
                orig.FontFace = mod.FontFace;
                orig.FontFaceSize = mod.FontFaceSize;
                orig.FilePath = mod.FilePath;
                orig.Label = mod.Label;
                orig.IconPath = mod.IconPath;
                orig.Icon = mod.Icon;
                orig.Order = mod.Order;
                orig.SplitRegex = mod.SplitRegex;
                orig.CommaCharacters = mod.CommaCharacters;
                orig.PeriodCharacters = mod.PeriodCharacters;
            }

            foreach (KeyValuePair<string, string> kvp in mod.Dialog) {
                orig.Dialog[kvp.Key] = kvp.Value;
            }

            foreach (KeyValuePair<string, string> kvp in mod.Cleaned) {
                orig.Cleaned[kvp.Key] = kvp.Value;
            }

            return orig;
        }

        [MonoModReplace]
        public static bool Has(string name, Language language = null) {
            if (string.IsNullOrEmpty(name))
                return true;

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
            if (string.IsNullOrEmpty(name))
                return "";

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
            if (string.IsNullOrEmpty(name))
                return "";

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
