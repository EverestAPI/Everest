﻿#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using EverestSplash;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System.Text.RegularExpressions;
using System.Text;
using System.Reflection;
using System.Runtime.Versioning;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Celeste {
    class patch_Celeste : Celeste {

        // We're effectively in Celeste, but still need to "expose" private fields to our mod.
        private bool firstLoad;

        // EverestSplash related, will be null when that is not loaded
        private static NamedPipeServerStream splashPipeServerStream;
        private static Task splashPipeServerStreamConnection;

        [PatchCelesteMain]
        public static extern void orig_Main(string[] args);
        [MonoModPublic]
        public static void Main(string[] args) {
            if (Thread.CurrentThread.Name != "Main Thread") {
                Thread.CurrentThread.Name = "Main Thread";
            }

            if (File.Exists("BuildIsXNA.txt"))
                File.Delete("BuildIsXNA.txt");
            if (File.Exists("BuildIsFNA.txt"))
                File.Delete("BuildIsFNA.txt");

            // we cannot use Everest.Flags.IsFNA at this point because flags aren't initialized yet.
            File.WriteAllText($"BuildIs{(typeof(Game).Assembly.FullName.Contains("FNA") ? "FNA" : "XNA")}.txt", "");

            if (File.Exists("everest-launch.txt")) {
                args =
                    File.ReadAllLines("everest-launch.txt")
                    .Select(l => l.Trim())
                    .Where(l => !l.StartsWith("#"))
                    .SelectMany(l => l.Split(' '))
                    .Concat(args)
                    .ToArray();
            } else {
                using (StreamWriter writer = File.CreateText("everest-launch.txt")) {
                    writer.WriteLine("# Add any Everest launch flags here.");
                    writer.WriteLine("# Lines starting with # are ignored.");
                    writer.WriteLine("# All options here are disabled by default.");
                    writer.WriteLine("# Full list: https://github.com/EverestAPI/Resources/wiki/Command-Line-Arguments");
                    writer.WriteLine();
                    writer.WriteLine("# Windows only: open a separate log console window.");
                    writer.WriteLine("#--console");
                    writer.WriteLine();
                    writer.WriteLine("# FNA only: force OpenGL (might be necessary to bypass a load crash on some PCs).");
                    writer.WriteLine("#--graphics OpenGL");
                    writer.WriteLine();
                    writer.WriteLine("# Change default log level (verbose will print all logs).");
                    writer.WriteLine("#--loglevel verbose");

                    if (File.Exists("launch.txt")) {
                        using (StreamReader reader = File.OpenText("launch.txt")) {
                            writer.WriteLine();
                            writer.WriteLine();
                            writer.WriteLine("# The following options are migrated from the old launch.txt and force-disabled.");
                            writer.WriteLine("# Some of them might not work anymore or cause unwanted effects.");
                            writer.WriteLine();
                            writer.WriteLine();
                            for (string line; (line = reader.ReadLine()) != null;) {
                                writer.Write("#");
                                writer.WriteLine(line);
                            }
                        }
                        File.Delete("launch.txt");
                    }
                }
            }

            if (File.Exists("everest-env.txt")) {
                foreach (string line in File.ReadAllLines("everest-env.txt")) {
                    if (line.StartsWith("#"))
                        continue;

                    int index = line.IndexOf('=');
                    if (index == -1)
                        continue;

                    string key = line.Substring(0, index).Trim();
                    if (key.StartsWith("'") && key.EndsWith("'"))
                        key = key.Substring(1, key.Length - 2);

                    string value = line.Substring(index + 1).Trim();
                    if (value.StartsWith("'") && value.EndsWith("'"))
                        value = value.Substring(1, value.Length - 2);

                    if (key.EndsWith("!")) {
                        key = key.Substring(0, key.Length - 1);
                    } else {
                        value = value
                            .Replace("\\r", "\r")
                            .Replace("\\n", "\n")
                            .Replace($"${{{key}}}", Environment.GetEnvironmentVariable(key) ?? "");
                    }

                    Environment.SetEnvironmentVariable(key, value);
                }
            }

            // Get the splash up and running asap
            if (!args.Contains("--disable-splash")) {
                Barrier barrier = new(2);
                string targetRenderer = "";
                for (int i = 0; i < args.Length; i++) { // The splash will use the same renderer as fna
                    if (args[i] == "--graphics" && args.Length > i + 1) {
                        targetRenderer = args[i + 1];
                    }
                }
                
                // We require that the sdl_init happens synchronously but on the thread where its going to be used
                // its not documented anywhere that this is dangerous, so danger is assumed
                Thread thread = new(() => {
                    EverestSplashWindow window = EverestSplash.EverestSplash.CreateWindow(targetRenderer);
                    
                    barrier.SignalAndWait();
                    EverestSplash.EverestSplash.RunWindow(window);
                });
                thread.Start();
                barrier.SignalAndWait();
                
                splashPipeServerStream = new NamedPipeServerStream(EverestSplash.EverestSplash.Name);
                splashPipeServerStreamConnection = splashPipeServerStream.WaitForConnectionAsync();    
            }

            if (args.Contains("--console") && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                AllocConsole();

                // Invalidate console streams
                typeof(Console).GetField("s_in", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
                typeof(Console).GetField("s_out", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
                typeof(Console).GetField("s_error", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
            }

            if (args.Contains("--nolog")) {
                MainInner(args);
                return;
            }

            string logfile = Environment.GetEnvironmentVariable("EVEREST_LOG_FILENAME") ?? "log.txt";

            // Only applying log rotation on default name, feel free to improve LogRotationHelper to deal with custom log file names...
            if (logfile == "log.txt" && File.Exists("log.txt")) {
                if (new FileInfo("log.txt").Length > 0) {
                    // move the old log.txt to the LogHistory folder.
                    // note that the cleanup will only be done when the core module is loaded: the settings aren't even loaded right now,
                    // so we don't know how many files we should keep.
                    if (!Directory.Exists("LogHistory")) {
                        Directory.CreateDirectory("LogHistory");
                    }
                    File.Move("log.txt", Path.Combine("LogHistory", LogRotationHelper.GetFileNameByDate(File.GetLastWriteTime("log.txt"))));
                } else {
                    // log is empty! (this actually happens more often than you'd think, because of Steam re-opening Celeste)
                    // just delete it.
                    File.Delete("log.txt");
                }
            } else {
                // check if log filename is allowed
                Regex regexBadCharacter = new Regex("[" + Regex.Escape(new string(Path.GetInvalidFileNameChars())) + "]");
                Match match = regexBadCharacter.Match(logfile);

                if (match.Success) {
                    StringBuilder errorText = new StringBuilder($"Custom log filename set in EVEREST_LOG_FILENAME=\"{logfile}\" contains invalid character(s): ", 100);

                    while (match.Success) {
                        foreach (Capture c in match.Groups[0].Captures)
                            errorText.Append(c);

                        match = match.NextMatch();
                        if (match.Success)
                            errorText.Append(" ");
                    }

                    throw new ArgumentException(errorText.ToString());
                }

                if (!logfile.EndsWith(".txt"))
                    logfile += ".txt";
            }

            Everest.PathLog = logfile;

            using (Stream fileStream = new FileStream(logfile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            using (StreamWriter fileWriter = new StreamWriter(fileStream, Console.OutputEncoding))
            using (LogWriter logWriter = new LogWriter(Console.Out, Console.Error, fileWriter))
                MainInner(args);

        }

        private static void MainInner(string[] args) {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

            try {
                Everest.ParseArgs(args);
                ParseFNAArgs(args);
                orig_Main(args);
            } catch (Exception e) {
                CriticalFailureHandler(e);
                return;
            } finally {
                Instance?.Dispose();
            }

            Everest.Shutdown();
        }

        private static void ParseFNAArgs(string[] args) {
            // FNA's main function already does this, but it doesn't work on Linux because the runtime doesn't call setenv :catassault:

            static void SetEnvVar(string name, string value) {
                Environment.SetEnvironmentVariable(name, value);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    if (setenv(name, value, 1) != 0)
                        throw new Win32Exception();
            }

            SetEnvVar("FNA_AUDIO_DISABLE_SOUND", "1");
            for (int i = 0; i < args.Length; i++) {
                if (args[i] == "--graphics" && i < args.Length - 1) {
                    SetEnvVar("FNA3D_FORCE_DRIVER", args[i + 1]);
                    i++;
                }
                // No --disable-lateswaptear as that is now explicitly opt-in
            }
        }

        [SupportedOSPlatform("linux")]
        [DllImport("libc", CallingConvention=CallingConvention.Cdecl, SetLastError=true)]
        private extern static int setenv(string name, string val, int overwrite);

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e) {
            if (e.IsTerminating) {
                _CriticalFailureIsUnhandledException = true;
                CriticalFailureHandler(e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception"));

            } else {
                (e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception")).LogDetailed("UNHANDLED");
            }
        }

        private static bool _CriticalFailureIsUnhandledException;
        public static void CriticalFailureHandler(Exception e) {
            Everest.LogDetours();

            (e ?? new Exception("Unknown exception")).LogDetailed("CRITICAL");

            ErrorLog.Write(
@"Yo, I heard you like Everest so I put Everest in your Everest so you can Ever Rest while you Ever Rest.

In other words: Celeste has encountered a catastrophic failure.

IF YOU WANT TO HELP US FIX THIS:
Please join the Celeste Discord server and drag and drop your log.txt into #modding_help.
https://discord.gg/6qjaePQ");

            ErrorLog.Open();
            if (!_CriticalFailureIsUnhandledException)
                Environment.Exit(-1);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        // Patching constructors is ugly.
        public extern void orig_ctor_Celeste();
        [MonoModConstructor]
        [MonoModOriginalName("orig_ctor_Celeste")] // For Everest.Installer
        public void ctor() {
            // Everest.Flags aren't initialized this early.
            if (Environment.GetEnvironmentVariable("EVEREST_HEADLESS") == "1") {
                Instance = this;
                Version = new Version(0, 0, 0, 0);
                Console.WriteLine("CELESTE HEADLESS VIA EVEREST");
            } else {
                orig_ctor_Celeste();
            }

            Logger.Log(LogLevel.Info, "boot", $"Active compatibility mode: {Everest.CompatibilityMode}");

            try {
                Everest.Boot();
            } catch (Exception e) {
                e.LogDetailed();
                /*
                ErrorLog.Write(e);
                ErrorLog.Open();
                */
                throw;
            }
        }

        protected extern void orig_Initialize();
        protected override void Initialize() {
            // Note: You may instinctually call base.Initialize();
            // DON'T! The original method is orig_Initialize
            orig_Initialize();

            Everest.Initialize();
        }

        protected extern void orig_LoadContent();
        protected override void LoadContent() {
            // Note: You may instinctually call base.LoadContent();
            // DON'T! The original method is orig_LoadContent
            bool firstLoad = this.firstLoad;

            /* Vanilla calls GFX.Load and MTN.Load in LoadContent on non-Stadia platforms.
             * Sadly we can't load them in GameLoader.LoadThread as mods rely on them in LoadContent.
             *
             * Loading in a new thread with texture -> GPU ops on the main thread helps barely.
             * Spawning a new thread just to wait for it to end doesn't make much sense,
             * BUT delaying the slow texture load ops to happen lazy-async gets the game window to appear sooner.
             *
             * Note that on XNA, this dies both with and without threaded GL due to OOM exceptions.
             * -ade
             */

            if (CoreModule.Settings.FastTextureLoading ?? (Environment.ProcessorCount >= 4 && !(CoreModule.Settings.ThreadedGL ?? Everest.Flags.PreferThreadedGL))) {
                long limit = (long) (CoreModule.Settings.FastTextureLoadingMaxMB * 1024f * 1024f);

                if (limit <= 0) {
                    limit = (long) (Everest.SystemMemoryMB * 0.2f * 1024f * 1024f);
                    // Assume that even in the worst case with 4 GB system RAM, 512 MB (= 12.5% = 1/8) are still available for texture loads.
                    if (limit <= (512L * 1024L * 1024L))
                        limit = (512L * 1024L * 1024L);
                }
                // ... and even if the user forcibly lowered it below 128 MB, fall back to 128 MB as even the vanilla gameplay atlas is 64MB.
                if (limit <= (128L * 1024L * 1024L))
                    limit = (128L * 1024L * 1024L);

                patch_VirtualTexture.StartFastTextureLoading(limit);
            }

            orig_LoadContent();

            foreach (EverestModule mod in Everest._Modules)
                mod.LoadContent(firstLoad);

            patch_VirtualTexture.StopFastTextureLoading();

            Everest._ContentLoaded = true;
        }

        protected override void BeginRun() {
            // This is as close as we can get to the showwindow call
            base.BeginRun();
            if (splashPipeServerStream != null) {
                if (!splashPipeServerStreamConnection.IsCompleted) {
                    Console.WriteLine("Could not connect to splash");
                    return;
                }
                StreamWriter sw = new(splashPipeServerStream);
                sw.WriteLine("stop");
                sw.Flush();
                StreamReader sr = new(splashPipeServerStream);
                // yes, this, inevitably, slows down the everest boot process, but, see EverestSplashWindow.FeedBack
                // for more info
                sr.ReadLine();
            }
        }

        protected override void OnExiting(object sender, EventArgs args) {
            base.OnExiting(sender, args);
            Everest.Events.Celeste.Exiting();
        }

    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the original Celeste entry point instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchCelesteMain))]
    class PatchCelesteMainAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchCelesteMain(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);
            // TryGotoNext used because SDL_GetPlatform does not exist on XNA
            if (cursor.TryGotoNext(instr => instr.MatchCall("SDL2.SDL", "SDL_GetPlatform"))) {
                cursor.Next.OpCode = OpCodes.Ldstr;
                cursor.Next.Operand = "Windows";
            }
        }

    }
}
