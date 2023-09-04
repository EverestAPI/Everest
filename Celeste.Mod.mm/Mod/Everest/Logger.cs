using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using MonoMod.Utils;
using Celeste.Mod.Core;

namespace Celeste.Mod {
    public static class Logger {

        internal static LogColorMode colorMode = LogColorMode.Auto;
        private static bool useColors = false;
        // Console.Out will get mirrored to the log file, however we need to write to the log file ourselves,
        // to avoid including color escape sequences in it.
        internal static TextWriter outWriter;
        internal static TextWriter logWriter;

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
            lock (minimumLevels)
                minimumLevels[tagPrefix] = minimumLevel;

            lock (minimumLevelsCache)
                minimumLevelsCache.Clear();
        }

        // same as above, but for internal Everest use
        internal static void SetLogLevelFromSettings(string tagPrefix, LogLevel minimumLevel) {
            minimumLevelsFromEverestSettings[tagPrefix] = minimumLevel;

            lock (minimumLevelsCache)
                minimumLevelsCache.Clear();
        }

        /// <summary>
        /// Gets the minimum log level that will be written in log.txt for the given tag.
        /// </summary>
        /// <param name="tag">The tag to get the minimum log level for</param>
        /// <returns>The minimum log level for this tag</returns>
        public static LogLevel GetLogLevel(string tag) {
            lock (minimumLevelsCache) {
                if (minimumLevelsCache.TryGetValue(tag, out LogLevel cachedLevel)) {
                    return cachedLevel;
                }

                // look for the wanted log level in mod settings first, in rules set through code next.
                LogLevel? wantedLogLevel = findMatchingLogLevel(minimumLevelsFromEverestSettings, tag);
                if (!wantedLogLevel.HasValue) {
                    lock (minimumLevels)
                        wantedLogLevel = findMatchingLogLevel(minimumLevels, tag);
                }

                // cache and return it.
                minimumLevelsCache[tag] = wantedLogLevel ?? LogLevel.Info;
                return wantedLogLevel ?? LogLevel.Info;
            }
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
        /// Log a string to the console and to log.txt, using <see cref="LogLevel.Verbose"/>
        /// </summary>
        /// <param name="tag">The tag, preferably short enough to identify your mod, but not too long to clutter the log.</param>
        /// <param name="str">The string / message to log.</param>
        public static void Verbose(string tag, string str)
            => Log(LogLevel.Verbose, tag, str);
        /// <summary>
        /// Log a string to the console and to log.txt, using <see cref="LogLevel.Debug"/>
        /// </summary>
        /// <param name="tag">The tag, preferably short enough to identify your mod, but not too long to clutter the log.</param>
        /// <param name="str">The string / message to log.</param>
        public static void Debug(string tag, string str)
            => Log(LogLevel.Debug, tag, str);
        /// <summary>
        /// Log a string to the console and to log.txt, using <see cref="LogLevel.Info"/>
        /// </summary>
        /// <param name="tag">The tag, preferably short enough to identify your mod, but not too long to clutter the log.</param>
        /// <param name="str">The string / message to log.</param>
        public static void Info(string tag, string str)
            => Log(LogLevel.Info, tag, str);
        /// <summary>
        /// Log a string to the console and to log.txt, using <see cref="LogLevel.Warn"/>
        /// </summary>
        /// <param name="tag">The tag, preferably short enough to identify your mod, but not too long to clutter the log.</param>
        /// <param name="str">The string / message to log.</param>
        public static void Warn(string tag, string str)
            => Log(LogLevel.Warn, tag, str);
        /// <summary>
        /// Log a string to the console and to log.txt, using <see cref="LogLevel.Error"/>
        /// </summary>
        /// <param name="tag">The tag, preferably short enough to identify your mod, but not too long to clutter the log.</param>
        /// <param name="str">The string / message to log.</param>
        public static void Error(string tag, string str)
            => Log(LogLevel.Error, tag, str);

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
                if (!CoreModule.Settings.ColorizedLogging) {
                    // Despite what your IDE might be telling you, DO NOT omit the manual .ToString() call, as this will cause unnecessary boxing.
                    // On modern runtimes string interpolation is much smarter and omitting that call reduces allocations, but not on Framework.
                    Console.WriteLine($"({DateTime.Now.ToString()}) [Everest] [{level.FastToString()}] [{tag}] {str}");
                    return;
                }

                const string colorReset = "\x1b[0m";
                string colorLevel = level.GetAnsiEscapeCodeForLevel();
                string colorText = level.GetAnsiEscapeCodeForText();

                string now_str = DateTime.Now.ToString();
                string level_str = level.FastToString();
                outWriter.WriteLine($"({now_str}) [Everest] {colorLevel}[{level_str}] [{tag}] {colorText}{str}{colorReset}");
                logWriter.WriteLine($"({now_str}) [Everest] [{level_str}] [{tag}] {str}");
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
                if (!CoreModule.Settings.ColorizedLogging) {
                    Console.WriteLine(new StackTrace(1, true).ToString());
                    return;
                }

                const string colorReset = "\x1b[0m";
                string colorText = level.GetAnsiEscapeCodeForText();

                outWriter.WriteLine($"{colorText}{new StackTrace(1, true).ToString()}{colorReset}");
                logWriter.WriteLine(new StackTrace(1, true).ToString());
            }
        }

        /// <summary>
        /// Print the exception to the console, including extended loading / reflection data useful for mods.
        /// </summary>
        public static void LogDetailed(/*this*/ Exception e, string tag = null) {
            if (CoreModule.Settings.ColorizedLogging) {
                string colorText = LogLevel.Error.GetAnsiEscapeCodeForText();
                outWriter.Write(colorText);
            }

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

            if (CoreModule.Settings.ColorizedLogging) {
                const string colorReset = "\x1b[0m";
                outWriter.Write(colorReset);
            }
        }

        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        [DllImport("kernel32.dll")]
		private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

		[DllImport("kernel32.dll")]
		private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GetStdHandle(int nStdHandle);

        internal static void SetupColoredLogging() {
            if (colorMode == LogColorMode.On) {
                if (PlatformHelper.Is(MonoMod.Utils.Platform.Windows)) {
                    TryEnableWindowsVTSupport();
                }
                useColors = true;
            } else if (colorMode == LogColorMode.Off) {
                useColors = false;
            } else if (colorMode == LogColorMode.Auto) {
                // Autodetect wheather to use ANSI colors

                // Honor https://no-color.org
                if (Environment.GetEnvironmentVariable("NO_COLOR") != null) {
                    useColors = false;
                    return;
                }
                if (PlatformHelper.Is(MonoMod.Utils.Platform.Windows)) {
                    useColors = TryEnableWindowsVTSupport();
                }
                // On Unix most terminals support ANSI colors
                useColors = true;
            }
        }
        private static bool TryEnableWindowsVTSupport() {
            // Try to enable color support on Windows. Returns whether it was succesful.
            // Taken from https://github.com/steamcore/TinyLogger/blob/ee4de5369db75b4da259768c7950c2cb53be665d/src/TinyLogger/Console/AnsiSupport.cs#L10-L24
            var handle = GetStdHandle(STD_OUTPUT_HANDLE);

            if (!GetConsoleMode(handle, out var consoleMode)) {
                // Could fallback to slow Windows API if console mode can't be accessed, but we just disable colors
                return false;
            }

            if ((consoleMode & ENABLE_VIRTUAL_TERMINAL_PROCESSING) == 0) {
                SetConsoleMode(handle, consoleMode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
                return true;
            }
            return false;
        }
    }
    public enum LogLevel {
        Verbose,
        Debug,
        Info,
        Warn,
        Error
    }
    internal enum LogColorMode {
        On,
        Off,
        Auto,
    }

    public static class LogLevelExtensions {
        /// <summary>
        /// Converts this <see cref="LogLevel"/> to its string representation, in a way more performant than <see cref="Enum.ToString()"/>
        /// </summary>
        public static string FastToString(this LogLevel level) {
            return level switch {
                LogLevel.Verbose => nameof(LogLevel.Verbose),
                LogLevel.Debug => nameof(LogLevel.Debug),
                LogLevel.Info => nameof(LogLevel.Info),
                LogLevel.Warn => nameof(LogLevel.Warn),
                LogLevel.Error => nameof(LogLevel.Error),
                _ => level.ToString(),
            };
        }

        internal static string GetAnsiEscapeCodeForText(this LogLevel level) {
            return level switch {
                LogLevel.Verbose => "\x1b[95m",
                LogLevel.Debug => "\x1b[94m",
                LogLevel.Info => "",
                LogLevel.Warn => "\x1b[93m",
                LogLevel.Error => "\x1b[91m",
                _ => ""
            };
        }
        internal static string GetAnsiEscapeCodeForLevel(this LogLevel level) {
            return level switch {
                LogLevel.Verbose => "\x1b[35m",
                LogLevel.Debug => "\x1b[34m",
                LogLevel.Info => "",
                LogLevel.Warn => "\x1b[33m",
                LogLevel.Error => "\x1b[31m",
                _ => ""
            };
        }
    }
}
