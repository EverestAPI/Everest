using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Celeste.Mod.Helpers {
    public static class LogRotationHelper {
        public class OldestFirst : IComparer<string> {
            public int Compare(string first, string second) {
                DateTime firstDateTime, secondDateTime;
                ulong firstIndex, secondIndex;
                if (!TryParse(first, out firstDateTime, out firstIndex)
                    || !TryParse(second, out secondDateTime, out secondIndex)) {
                    // if one of their file names is invalid (or both), fallback to compare by alphabetical order
                    return string.Compare(first, second, StringComparison.Ordinal);
                }

                if (firstDateTime == secondDateTime) {
                    // compare by index if their datetime equals, smaller should be older
                    return firstIndex.CompareTo(secondIndex);
                }
                return firstDateTime.CompareTo(secondDateTime);
            }
        }

        public static string GetFileNameByDate(DateTime date) {
            string fileNameWithoutExtension = $"log_{date:yyyyMMdd_HHmmss}";
            List<string> sameDateFiles = Directory.GetFiles("LogHistory", fileNameWithoutExtension + "*.txt").ToList();
            if (sameDateFiles.Count == 0) {
                // index is not needed if there are no files with same date
                return fileNameWithoutExtension + ".txt";
            }
            // find the max index in all files with same date, and the new one will be max + 1
            ulong fileIndex = sameDateFiles.Max(fileName => TryParse(fileName, out _, out ulong index) ? index : 0) + 1;
            return $"{fileNameWithoutExtension}_{fileIndex}.txt";
        }

        private static bool TryParse(string path, out DateTime dateTime, out ulong index) {
            dateTime = DateTime.MinValue;
            index = 0;

            if (path == null) {
                return false;
            }

            string fileName = Path.GetFileNameWithoutExtension(path);

            // a valid file name should be log_<date>_<time>[_<index>].txt ([] means optional)
            Regex regex = new Regex(@"^log_(?<dateTime>\d{8}_\d{6})(?:_(?<index>\d+))?$");
            Match match = regex.Match(fileName);

            if (!match.Success) {
                return false;
            }
            if (!DateTime.TryParseExact(match.Groups["dateTime"].Value, "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime)) {
                return false;
            }
            bool hasIndex = match.Groups["index"].Success;
            if (!hasIndex) {
                // file name is log_<date>_<time>.txt and we set the index to 0
                index = 0;
                return true;
            }
            return ulong.TryParse(match.Groups["index"].Value, out index);
        }
    }
}