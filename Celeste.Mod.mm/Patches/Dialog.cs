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

        // We're effectively in Dialog, but still need to "expose" private fields to our mod.
        private static string[] LanguageDataVariables;
        private static readonly Regex command;
        private static readonly Regex insert;
        private static readonly Regex variable;

        private static Language FallbackLanguage;

        public static extern Language orig_LoadLanguage(string filename);
        public static Language LoadLanguage(string filename) {
            Language language = orig_LoadLanguage(filename);

            if (language.Id.Equals("english", StringComparison.InvariantCultureIgnoreCase))
                FallbackLanguage = language;

            string path = filename;
            if (path.StartsWith(Everest.Content.PathContentOrig))
                path = path.Substring(Everest.Content.PathContentOrig.Length + 1);
            path = path.Replace('\\', '/');
            if (path.EndsWith(".txt"))
                path = path.Substring(0, path.Length - 4);

            List<AssetMetadata> metas;
            if (!Everest.Content.TryGetDialogs(path, out metas) || metas.Count == 0)
                return language;

            foreach (AssetMetadata meta in metas) {
                string line;

                string currentName = "";
                StringBuilder builder = new StringBuilder();
                string prev = "";
                using (StreamReader reader = new StreamReader(meta.Stream))
                    while (reader.Peek() != -1) {
                        line = reader.ReadLine().Trim('\r', '\n').Trim();
                        
                        // The following is the original parser decompiled and formatted to our best understanding.

                        // ???
                        bool startsWithVariable = false;
                        foreach (string variable in LanguageDataVariables) {
                            if (!string.IsNullOrEmpty(variable) && line.StartsWith(variable, StringComparison.InvariantCultureIgnoreCase)) {
                                startsWithVariable = true;
                                break;
                            }
                        }
                        if (!startsWithVariable) {
                            line = Regex.Replace(line, @"\[unknown\]", @"", RegexOptions.IgnoreCase);
                            line = Regex.Replace(line, @"\[left\]", @"{left}", RegexOptions.IgnoreCase);
                            line = Regex.Replace(line, @"\[right\]", @"{right}", RegexOptions.IgnoreCase);
                            line = Regex.Replace(line, @"\[(?<content>[^\[\\]*(?:\\.[^\]\\]*)*)\]", @"{portrait ${content}}");
                        }

                        if (line.Length <= 0)
                            continue;
                        if (line[0] == '#')
                            continue;

                        line = line.Replace("\\#", "#");
                        bool isVariable = variable.IsMatch(line);
                        if (!isVariable) {
                            if (builder.Length > 0) {
                                string built = builder.ToString();
                                if (!built.EndsWith("{break}") && !built.EndsWith("{n}") && command.Replace(prev, "").Length > 0) {
                                    builder.Append("{break}");
                                }
                            }
                            builder.Append(line);
                            goto Next;
                        }

                        if (!string.IsNullOrEmpty(currentName) && !language.Dialog.ContainsKey(currentName)) {
                            language.Dialog.Add(currentName, builder.ToString());
                        }

                        string[] splitByEqual = line.Split('=');
                        string name = splitByEqual[0];
                        string value = (splitByEqual.Length > 1) ? splitByEqual[1].Trim() : "";

                        if (name.Equals("language", StringComparison.OrdinalIgnoreCase)) {
                            string[] splitByComma = value.Split(',');
                            if (!Dialog.Languages.TryGetValue(splitByComma[0], out language)) {
                                language = new Language();
                                language.FontFace = null;
                                language.Id = splitByComma[0];
                                Dialog.Languages.Add(language.Id, language);
                            }
                            if (splitByComma.Length > 1) {
                                language.Label = splitByComma[1];
                            }
                            goto Next;
                        }

                        if (name.Equals("icon", StringComparison.OrdinalIgnoreCase)) {
                            VirtualTexture texture = VirtualContent.CreateTexture(Path.Combine("Dialog", value));
                            language.Icon = new MTexture(texture);
                            goto Next;
                        }

                        if (name.Equals("order", StringComparison.OrdinalIgnoreCase)) {
                            language.Order = int.Parse(value);
                            goto Next;
                        }

                        if (name.Equals("font", StringComparison.OrdinalIgnoreCase)) {
                            string[] splitByComma = value.Split(',');
                            language.FontFace = splitByComma[0];
                            language.FontFaceSize = float.Parse(splitByComma[1], CultureInfo.InvariantCulture);
                            goto Next;
                        }

                        if (name.Equals("SPLIT_REGEX", StringComparison.OrdinalIgnoreCase)) {
                            language.SplitRegex = value;
                            goto Next;
                        }

                        if (name.Equals("commas", StringComparison.OrdinalIgnoreCase)) {
                            language.CommaCharacters = value;
                            goto Next;
                        }

                        if (name.Equals("periods", StringComparison.OrdinalIgnoreCase)) {
                            language.PeriodCharacters = value;
                            goto Next;
                        }

                        currentName = name;
                        builder.Clear();
                        builder.Append(value);

                        Next:
                        prev = line;

                    }

                if (!string.IsNullOrEmpty(currentName) && !language.Dialog.ContainsKey(currentName)) {
                    language.Dialog.Add(currentName, builder.ToString());
                }
            }

            return language;
        }

        public static extern void orig_InitLanguages();
        public static void InitLanguages() {
            orig_InitLanguages();
            Everest.Events.Dialog.InitLanguages();
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
            string result;
            if (string.IsNullOrEmpty(name)) {
                result = Dialog.Clean("levelset_");
            } else {
                result = name;
                result = ("levelset_" + result).DialogCleanOrNull() ?? result.DialogCleanOrNull() ?? result.SpacedPascalCase();
            }
            return result;
        }

    }
    public static class DialogExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static string CleanLevelSet(string name)
            => patch_Dialog.CleanLevelSet(name);

    }
}
