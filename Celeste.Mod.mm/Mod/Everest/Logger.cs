using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Celeste.Mod {
    public static class Logger {

        private static Dictionary<string, LogLevel> minimumLevels = new Dictionary<string, LogLevel>();
        private static Dictionary<string, LogLevel> minimumLevelsFromEverestSettings = new Dictionary<string, LogLevel>();
        private static Dictionary<string, LogLevel> minimumLevelsCache = new Dictionary<string, LogLevel>();

        /// <summary>
        /// Sets the minimum log level to be written in the logs for lines matching the given tag prefix.
        /// <br />
        /// When using this, make sure the tag prefix is restrictive enough not to impact other mods
        /// (for example, if all your tags follow the format MyMod/xxx, use "MyMod/" as a prefix).
        /// </summary>
        /// <param name="tagPrefix">The prefix of the log tags to affect with this log level</param>
        /// <param name="minimumLevel">The minimum level of logs to print out in the logs</param>
        public static void SetLogLevel(string tagPrefix, LogLevel minimumLevel) {
            minimumLevels[tagPrefix] = minimumLevel;
            minimumLevelsCache.Clear();
        }

        // same as above, but for internal Everest use
        internal static void SetLogLevelFromYaml(string tagPrefix, LogLevel minimumLevel) {
            minimumLevelsFromEverestSettings[tagPrefix] = minimumLevel;
            minimumLevelsCache.Clear();
        }

        /// <summary>
        /// Gets the minimum log level that will be written in log.txt for the given tag.
        /// </summary>
        /// <param name="tag">The tag to get the minimum log level for</param>
        /// <returns>The minimum log level for this tag</returns>
        public static LogLevel GetLogLevel(string tag) {
            if (minimumLevelsCache.TryGetValue(tag, out LogLevel cachedLevel)) {
                return cachedLevel;
            }

            // look for the wanted log level in mod settings first, in rules set through code next.
            LogLevel? wantedLogLevel = findMatchingLogLevel(minimumLevelsFromEverestSettings, tag);
            if (!wantedLogLevel.HasValue) {
                wantedLogLevel = findMatchingLogLevel(minimumLevels, tag);
            }

            // cache and return it.
            minimumLevelsCache[tag] = wantedLogLevel ?? LogLevel.Verbose;
            return wantedLogLevel ?? LogLevel.Verbose;
        }

        private static LogLevel? findMatchingLogLevel(Dictionary<string, LogLevel> rules, string tag) {
            // take the rules in reverse alphabetical order, so that the longest ones come first.
            List<string> prefixes = rules.Keys.ToList();
            prefixes.Sort((a, b) => b.CompareTo(a));

            // try matching all rules.
            foreach (string prefix in prefixes) {
                if (tag.StartsWith(prefix)) {
                    return rules[prefix];
                }
            }

            // none matched
            return null;
        }

        private static bool shouldLog(string tag, LogLevel level) {
            return GetLogLevel(tag) <= level;
        }

        // TODO: Allow displaying mod log in future ImGui UI

        /// <summary>
        /// Log a string to the console and to log.txt
        /// </summary>
        /// <param name="tag">The tag, preferably short enough to identify your mod, but not too long to clutter the log.</param>
        /// <param name="str">The string / message to log.</param>
        public static void Log(string tag, string str)
            => Log(LogLevel.Verbose, tag, str);
        /// <summary>
        /// Log a string to the console and to log.txt
        /// </summary>
        /// <param name="level">The log level.</param>
        /// <param name="tag">The tag, preferably short enough to identify your mod, but not too long to clutter the log.</param>
        /// <param name="str">The string / message to log.</param>
        public static void Log(LogLevel level, string tag, string str) {
            if (shouldLog(tag, level)) {
                Console.Write("(");
                Console.Write(DateTime.Now);
                Console.Write(") [Everest] [");
                Console.Write(level.ToString());
                Console.Write("] [");
                Console.Write(tag);
                Console.Write("] ");
                Console.WriteLine(str);
            }
        }

        /// <summary>
        /// Log a string to the console and to log.txt, including a call stack trace.
        /// </summary>
        /// <param name="tag">The tag, preferably short enough to identify your mod, but not too long to clutter the log.</param>
        /// <param name="str">The string / message to log.</param>
        public static void LogDetailed(string tag, string str)
            => LogDetailed(LogLevel.Verbose, tag, str);

        /// <summary>
        /// Log a string to the console and to log.txt, including a call stack trace.
        /// </summary>
        /// <param name="level">The log level.</param>
        /// <param name="tag">The tag, preferably short enough to identify your mod, but not too long to clutter the log.</param>
        /// <param name="str">The string / message to log.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LogDetailed(LogLevel level, string tag, string str) {
            if (shouldLog(tag, level)) {
                Log(level, tag, str);
                Console.WriteLine(new StackTrace(1, true).ToString());
            }
        }

        /// <summary>
        /// Print the exception to the console, including extended loading / reflection data useful for mods.
        /// </summary>
        public static void LogDetailed(/*this*/ Exception e, string tag = null) {
            if (tag == null) {
                Console.WriteLine("--------------------------------");
                Console.WriteLine("Detailed exception log:");
            }
            for (Exception e_ = e; e_ != null; e_ = e_.InnerException) {
                Console.WriteLine("--------------------------------");
                Console.WriteLine(e_.GetType().FullName + ": " + e_.Message + "\n" + e_.StackTrace);
                if (e_ is ReflectionTypeLoadException rtle) {
                    for (int i = 0; i < rtle.Types.Length; i++) {
                        Console.WriteLine("ReflectionTypeLoadException.Types[" + i + "]: " + rtle.Types[i]);
                    }
                    for (int i = 0; i < rtle.LoaderExceptions.Length; i++) {
                        LogDetailed(rtle.LoaderExceptions[i], tag + (tag == null ? "" : ", ") + "rtle:" + i);
                    }
                }
                if (e_ is TypeLoadException) {
                    Console.WriteLine("TypeLoadException.TypeName: " + ((TypeLoadException) e_).TypeName);
                }
                if (e_ is BadImageFormatException) {
                    Console.WriteLine("BadImageFormatException.FileName: " + ((BadImageFormatException) e_).FileName);
                }
            }
        }

    }
    public enum LogLevel {
        Verbose,
        Debug,
        Info,
        Warn,
        Error
    }
}
