#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Celeste {
    class patch_Language : Language {

        internal static Language LoadingLanguage;
        internal static bool LoadOrigLanguage;
        internal static bool LoadModLanguage;

        internal Dictionary<string, string> LineSources;
        internal Dictionary<string, int> ReadCount;
        internal string CurrentlyReadingFrom;

        [MonoModIgnore]
        [PatchLoadLanguage]
        public static extern new Language FromTxt(string path);

        public static Language FromModExport(ModAsset asset) {
            Language lang = new Language();

            using (BinaryReader reader = new BinaryReader(asset.Stream)) {
                lang.Id = reader.ReadString();
                lang.Label = reader.ReadString();

                lang.IconPath = reader.ReadString();
                lang.Icon = new MTexture(VirtualContent.CreateTexture(Path.Combine("Dialog", lang.IconPath)));

                lang.Order = reader.ReadInt32();

                lang.FontFace = reader.ReadString();
                lang.FontFaceSize = reader.ReadSingle();

                lang.SplitRegex = reader.ReadString();
                lang.CommaCharacters = reader.ReadString();
                lang.PeriodCharacters = reader.ReadString();

                lang.Lines = reader.ReadInt32();
                lang.Words = reader.ReadInt32();

                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++) {
                    string key = reader.ReadString();
                    lang.Dialog[key] = reader.ReadString();
                    lang.Cleaned[key] = reader.ReadString();
                }
            }

            return lang;
        }

        private static IEnumerable<string> _GetLanguageText(string path, Encoding encoding) {
            patch_Language lang = (patch_Language) _NewLanguage();

            bool ready = LoadOrigLanguage && File.Exists(path);
            if (ready) {
                lang.CurrentlyReadingFrom = "Celeste";
                foreach (string text in File.ReadLines(path, encoding))
                    yield return text;
            }

            path = path.Substring(Everest.Content.PathContentOrig.Length + 1);
            path = path.Replace('\\', '/');
            path = path.Substring(0, path.Length - 4);
            string dummy = $"LANGUAGE={path.Substring(7).ToLowerInvariant()}";

            if (!ready) {
                ready = true;
                // Feed a dummy language line. All empty languages are removed afterwards.
                yield return dummy;
            }

            if (!LoadModLanguage)
                yield break;

            foreach (ModContent content in Everest.Content.Mods) {
                foreach (ModAsset asset in content.Map
                    .Where(entry => entry.Value.Type == typeof(AssetTypeDialog) && entry.Key.Equals(path, StringComparison.InvariantCultureIgnoreCase))
                    .Select(entry => entry.Value)) {

                    lang.CurrentlyReadingFrom = asset.Source?.Name ?? "???";
                    using (StreamReader reader = new StreamReader(asset.Stream, encoding))
                        while (reader.Peek() != -1)
                            yield return reader.ReadLine().Trim('\r', '\n').Trim();

                    // Feed a new key to be sure that the last key in the file is cut off.
                    // That will prevent mod B from corrupting the last key of mod A if its language txt is bad.
                    lang.CurrentlyReadingFrom = null;
                    yield return "EVEREST_SPLIT_BETWEEN_FILES= New file";
                }
            }
        }

        private static Language _NewLanguage() {
            return LoadingLanguage ?? (LoadingLanguage = new Language());
        }

        private static void _SetItem(Dictionary<string, string> dict, string key, string value, Language _lang) {
            patch_Language lang = (patch_Language) _lang;

            if (lang.Dialog != dict || lang.ReadCount == null ||
                string.IsNullOrEmpty(lang.CurrentlyReadingFrom) ||
                key == "EVEREST_SPLIT_BETWEEN_FILES") {
                // Skip conflict checking when the dictionary is from an unknown source.

            } else {
                if (!lang.ReadCount.TryGetValue(key, out int count))
                    count = lang.Dialog.ContainsKey(key) ? 1 : 0;
                count++;
                lang.ReadCount[key] = count;

                if (!lang.LineSources.TryGetValue(key, out string sourcePrev))
                    sourcePrev = "?!?!?!";
                lang.LineSources[key] = lang.CurrentlyReadingFrom;

                if (count >= 2)
                    Logger.Log(LogLevel.Warn, "Language", $"Conflict for dialog key {lang.Id}/{key} ({sourcePrev} vs {lang.CurrentlyReadingFrom})");
            }


            dict[key] = value;
        }

    }
}
