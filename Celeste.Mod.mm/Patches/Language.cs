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
    class patch_Language : Language {

        internal static Language LoadingLanguage;
        internal static bool LoadOrigLanguage;
        internal static bool LoadModLanguage;

        internal HashSet<string> AllowConflictDuringMerge;
        internal Dictionary<string, int> SetCount;

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
            bool ready = LoadOrigLanguage && File.Exists(path);
            if (ready)
                foreach (string text in File.ReadLines(path, encoding))
                    yield return text;

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

            foreach (ModAsset asset in
                Everest.Content.Mods
                .Select(mod => mod.Map.TryGetValue(path, out ModAsset asset) ? asset : null)
                .Where(asset => asset != null && asset.Type == typeof(AssetTypeDialog))
            ) {

                using (StreamReader reader = new StreamReader(asset.Stream, encoding))
                    while (reader.Peek() != -1)
                        yield return reader.ReadLine().Trim('\r', '\n').Trim();

                // Feed a new key to be sure that the last key in the file is cut off.
                // That will prevent mod B from corrupting the last key of mod A if its language txt is bad.
                yield return "EVEREST_SPLIT_BETWEEN_FILES= New file";
            }
        }

        private static Language _NewLanguage() {
            return LoadingLanguage ?? (LoadingLanguage = new Language());
        }

        private static void _SetItem(Dictionary<string, string> dict, string key, string value, Language _lang) {
            patch_Language lang = (patch_Language) _lang;

            if (key != "EVEREST_SPLIT_BETWEEN_FILES") {
                if (lang.Dialog != dict || lang.SetCount == null) {
                    // Skip conflict checking when the dictionary is from an unknown source.

                } else if (dict.ContainsKey(key)) {
                    // Each key is set at least twice: During actual read and during variable filling.
                    // If it's set more than twice (no matter if during read or fillup), there's a conflict.
                    if (!lang.SetCount.TryGetValue(key, out int count))
                        count = 0;
                    count++;
                    lang.SetCount[key] = count;
                    if (count >= 2)
                        Logger.Log(LogLevel.Warn, "Language", $"Conflict for dialog key {lang.Id}/{key} (read)");
                }
            }


            dict[key] = value;
        }

    }
}
