#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Celeste {
    partial class patch_Language : Language {

        internal static Language LoadingLanguage;
        internal static bool LoadOrigLanguage;
        internal static bool LoadModLanguage;

        internal Dictionary<string, string> LineSources;
        internal Dictionary<string, int> ReadCount;
        internal string CurrentlyReadingFrom;

        [GeneratedRegex(@"^(?:\{[^}]*\}\s*)*$")]
        private static partial Regex WholeLineIsCommandsRegex();

        [GeneratedRegex(@"\{(.*?)\}", RegexOptions.RightToLeft)]
        private static partial Regex CommandRegex();
        
        [GeneratedRegex(@"\[(?<content>[^\[\\]*(?:\\.[^\]\\]*)*)\]", RegexOptions.IgnoreCase)]
        private static partial Regex PortraitRegex();
        
        [GeneratedRegex(@"^\w+\=.*")]
        private static partial Regex VariableRegex();
        
        [GeneratedRegex(@"\{\+\s*(.*?)\}")]
        private static partial Regex InsertRegex();

        /// <summary>
        /// Splits text like 'key=value' into two spans.
        /// If the separator is not found, 'left' contains the entire string and 'right' is empty.
        /// </summary>
        private static bool SplitPair(ReadOnlySpan<char> from, char separator, out ReadOnlySpan<char> left, out ReadOnlySpan<char> right) {
            int idx = from.IndexOf(separator);
            if (idx == -1) {
                left = from;
                right = Span<char>.Empty;
                return false;
            }
            
            left = from[..idx];
            right = from[(idx + 1)..].Trim();
            return true;
        }
        
        [MonoModReplace] // Rewrite the method to optimise it and fix issues with multiple equals signs being in the same line.
        public new static Language FromTxt(string path) {
            Language language = null;
            string nextKey = "";
            StringBuilder nextEntryBuilder = new();
            string prevLine = "";
            ReadOnlySpan<char> lastAddedNonEmptyLine = "";
            
            foreach (string lineUntrimmed in _GetLanguageText(path, Encoding.UTF8)) {
                var line = lineUntrimmed.Trim();
                if (line.Length <= 0 || line[0] == '#') {
                    continue;
                }

                if (line.IndexOf('[') >= 0) {
                    line = PortraitRegex().Replace(line, "{portrait ${content}}");
                }

                line = line.Replace("\\#", "#", StringComparison.Ordinal);
                if (line.Length <= 0) {
                    continue;
                }

                // See if this line starts a new dialog key
                if (VariableRegex().IsMatch(line)) {
                    if (!string.IsNullOrEmpty(nextKey)) {
                        // end the previous dialog key
                        _SetItem(language.Dialog, nextKey, nextEntryBuilder.ToString(), language);
                    }

                    SplitPair(line, '=', out var cmd, out var argument);
                    
                    if (cmd.Equals("language", StringComparison.OrdinalIgnoreCase)) {
                        language = _NewLanguage();
                        language.FontFace = null;
                        language.FilePath = Path.GetFileName(path);

                        if (SplitPair(argument, ',', out var id, out var label)) {
                            language.Id = id.ToString();
                            language.Label = label.ToString();
                        } else {
                            language.Id = argument.ToString();
                        }
                    } else if (cmd.Equals("icon", StringComparison.OrdinalIgnoreCase)) {
                        string argStr = argument.ToString();
                        VirtualTexture texture = VirtualContent.CreateTexture(Path.Combine("Dialog", argStr));
                        language.IconPath = argStr;
                        language.Icon = new MTexture(texture);
                    } else if (cmd.Equals("order", StringComparison.OrdinalIgnoreCase)) {
                        language.Order = int.Parse(argument);
                    } else if (cmd.Equals("font", StringComparison.OrdinalIgnoreCase)) {
                        if (SplitPair(argument, ',', out var face, out var faceSize)) {
                            language.FontFace = face.ToString();
                            language.FontFaceSize = float.Parse(faceSize, CultureInfo.InvariantCulture);
                        }
                    } else if (cmd.Equals("SPLIT_REGEX", StringComparison.OrdinalIgnoreCase)) {
                        language.SplitRegex = argument.ToString();
                    } else if (cmd.Equals("commas", StringComparison.OrdinalIgnoreCase)) {
                        language.CommaCharacters = argument.ToString();
                    } else if (cmd.Equals("periods", StringComparison.OrdinalIgnoreCase)) {
                        language.PeriodCharacters = argument.ToString();
                    } else {
                        // This is just a normal dialog.
                        // By this point, we've already added the previous entry to the Dialog dictionary.
                        nextKey = cmd.ToString();
                        nextEntryBuilder.Clear();
                        nextEntryBuilder.Append(argument);
                        lastAddedNonEmptyLine = argument;
                    }
                } else {
                    // Continue the previously started dialog
                    
                    if (nextEntryBuilder.Length > 0) {
                        // Auto-add linebreaks if the previous line wasn't entirely commands and had no line break commands.
                        if (!lastAddedNonEmptyLine.EndsWith("{break}", StringComparison.Ordinal)
                            && !lastAddedNonEmptyLine.EndsWith("{n}", StringComparison.Ordinal)
                            && !WholeLineIsCommandsRegex().IsMatch(prevLine)
                        ) {
                            nextEntryBuilder.Append("{break}");
                            lastAddedNonEmptyLine = "{break}";
                        }
                    }

                    nextEntryBuilder.Append(line);
                    lastAddedNonEmptyLine = line.Length > 0 ? line : lastAddedNonEmptyLine;
                }

                prevLine = line;
            }

            // Make sure to add the final key in the lang file
            if (!string.IsNullOrEmpty(nextKey)) {
                _SetItem(language.Dialog, nextKey, nextEntryBuilder.ToString(), language);
            }

            var keys = language.Dialog.Keys;

            // Handle {+DIALOG_ID} constructs, recursively
            foreach (string key in keys) {
                string dialog = GetDialogWithResolvedInserts(language, language.Dialog[key]);
                _SetItem(language.Dialog, key, dialog, language);
                
                static string GetDialogWithResolvedInserts(Language language, string dialog) {
                    return InsertRegex().Replace(dialog, match => {
                        string keyToReplaceWith = match.Groups[1].Value;

                        return GetDialogWithResolvedInserts(language, language.Dialog.GetValueOrDefault(keyToReplaceWith, "[XXX]"));
                    });
                }
            }

            language.Lines = 0;
            language.Words = 0;
            
            // Create cleaned entries
            foreach (string key in keys) {
                string dialog = language.Dialog[key];

                if (dialog.Contains('{')) {
                    dialog = CommandRegex().Replace(dialog, match => match.ValueSpan is "{n}" or "{break}" ? "\n" : "");
                }
                
                language.Cleaned[key] = dialog;
            }

            return language;
        }

        public static extern Language orig_FromExport(string path);
        public static new Language FromExport(string path) {
            Language lang = orig_FromExport(path);
            lang.FilePath = Path.GetFileNameWithoutExtension(path);
            return lang;
        }

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

            lang.FilePath = Path.GetFileNameWithoutExtension(asset.PathVirtual);
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

            path = path[(Everest.Content.PathContentOrig.Length + 1)..];
            path = path.Replace('\\', '/');
            path = path[..^".txt".Length];

            if (!ready) {
                ready = true;
                // Feed a dummy language line. All empty languages are removed afterwards.
                yield return $"LANGUAGE={path["Dialog/".Length..].ToLowerInvariant()}";
            }

            if (!LoadModLanguage)
                yield break;

            foreach (ModContent content in Everest.Content.Mods) {
                foreach (ModAsset asset in content.Map
                    .Where(entry => entry.Value.Type == typeof(AssetTypeDialog) && entry.Key.Equals(path, StringComparison.OrdinalIgnoreCase))
                    .Select(entry => entry.Value)) {

                    lang.CurrentlyReadingFrom = asset.Source?.Name ?? "???";
                    using (StreamReader reader = new StreamReader(asset.Stream, encoding))
                        while (reader.Peek() != -1)
                            yield return reader.ReadLine().Trim();

                    // Feed a new key to be sure that the last key in the file is cut off.
                    // That will prevent mod B from corrupting the last key of mod A if its language txt is bad.
                    lang.CurrentlyReadingFrom = null;
                    yield return "EVEREST_SPLIT_BETWEEN_FILES= New file";
                }
            }
        }

        private static Language _NewLanguage() {
            return LoadingLanguage ??= new Language();
        }

        private static void _SetItem(Dictionary<string, string> dict, string key, string value, Language _lang) {
            patch_Language lang = (patch_Language) _lang;

            if (lang.Dialog != dict || lang.ReadCount == null ||
                string.IsNullOrEmpty(lang.CurrentlyReadingFrom) ||
                key == "EVEREST_SPLIT_BETWEEN_FILES") {
                // Skip conflict checking when the dictionary is from an unknown source.

            } else {
                ref int count = ref CollectionsMarshal.GetValueRefOrAddDefault(lang.ReadCount, key, out bool existed);
                if (!existed)
                    count = lang.Dialog.ContainsKey(key) ? 1 : 0;
                count++;

                string sourcePrev = lang.LineSources.GetValueOrDefault(key, "?!?!?!");
                lang.LineSources[key] = lang.CurrentlyReadingFrom;

                if (count >= 2)
                    Logger.Warn("Language", $"Conflict for dialog key {lang.Id}/{key} ({sourcePrev} vs {lang.CurrentlyReadingFrom})");
            }


            dict[key] = value;
        }

    }
}
