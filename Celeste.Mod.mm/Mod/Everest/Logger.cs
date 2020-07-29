using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static class Logger {

        private static Dictionary<Regex, LogLevel> minimumLevels = new Dictionary<Regex, LogLevel>();
        private static Dictionary<Regex, LogLevel> minimumLevelsFromEverestSettings = new Dictionary<Regex, LogLevel>();
        private static Dictionary<string, LogLevel> minimumLevelsCache = new Dictionary<string, LogLevel>();

        /// <summary>
        /// Sets the minimum log level to be written in the logs for lines matching the given tag.
        /// When using this, make sure the tag regex is restrictive enough not to impact other mods
        /// (for example, if all your tags follow the format MyMod/xxx, use "^MyMod/" as a regex).
        /// </summary>
        /// <param name="tagRegex">A regex matching the tags to affect with this log level</param>
        /// <param name="minimumLevel">The minimum level of logs to print out in the logs</param>
        /// (useful to make levels set by code overridable through modsettings-Everest.celeste)</param>
        public static void SetLogLevel(Regex tagRegex, LogLevel minimumLevel) {
            minimumLevels[tagRegex] = minimumLevel;
            minimumLevelsCache.Clear();
        }

        // same as above, but for internal Everest use
        internal static void SetLogLevelFromYaml(Regex tagRegex, LogLevel minimumLevel) {
            minimumLevelsFromEverestSettings[tagRegex] = minimumLevel;
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

            LogLevel? wantedLogLevel = null;

            // check log levels defined in mod settings.
            foreach (Regex regex in minimumLevelsFromEverestSettings.Keys) {
                if (regex.IsMatch(tag)) {
                    wantedLogLevel = minimumLevelsFromEverestSettings[regex];
                    break;
                }
            }

            if (!wantedLogLevel.HasValue) {
                // no log level defined in mod settings matches - check log levels defined through code.
                foreach (Regex regex in minimumLevels.Keys) {
                    if (regex.IsMatch(tag)) {
                        wantedLogLevel = minimumLevels[regex];
                        break;
                    }
                }
            }

            minimumLevelsCache[tag] = wantedLogLevel ?? LogLevel.Verbose;
            return wantedLogLevel ?? LogLevel.Verbose;
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
                if (e_ is ReflectionTypeLoadException) {
                    ReflectionTypeLoadException rtle = (ReflectionTypeLoadException) e_;
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
