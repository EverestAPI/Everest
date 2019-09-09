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

        public static extern Language orig_FromTxt(string path);
        [PatchLoadLanguage]
        public static new Language FromTxt(string path) {
            Language lang = orig_FromTxt(path);
            LoadingLanguage = null;
            return lang;
        }

        private static IEnumerable<string> _GetLanguageText(string path, Encoding encoding) {
            bool ready = File.Exists(path);
            if (ready)
                foreach (string text in File.ReadLines(path, encoding))
                    yield return text;

            path = path.Substring(Everest.Content.PathContentOrig.Length + 1);
            path = path.Replace('\\', '/');
            path = path.Substring(0, path.Length - 4);
            string dummy = $"LANGUAGE={path.Substring(7).ToLowerInvariant()}";
            foreach (ModAsset asset in
                Everest.Content.Mods
                .Select(mod => mod.Map.TryGetValue(path, out ModAsset asset) ? asset : null)
                .Where(asset => asset != null)
            ) {
                if (!ready) {
                    ready = true;
                    // Feed a dummy language line. All empty languages are removed afterwards.
                    yield return dummy;
                }
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

    }
}
