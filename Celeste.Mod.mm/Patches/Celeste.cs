#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste {
    class patch_Celeste : Celeste {

        // We're effectively in Celeste, but still need to "expose" private fields to our mod.
        private bool firstLoad;

        [PatchCelesteMain]
        public static extern void orig_Main(string[] args);
        [MonoModPublic]
        public static void Main(string[] args) {
            if (Thread.CurrentThread.Name != "Main Thread") {
                Thread.CurrentThread.Name = "Main Thread";
            }

            if (File.Exists("launch.txt")) {
                args =
                    File.ReadAllLines("launch.txt")
                    .Select(l => l.Trim())
                    .Where(l => !l.StartsWith("#"))
                    .SelectMany(l => l.Split(' '))
                    .Concat(args)
                    .ToArray();
            } else {
                using (StreamWriter writer = File.CreateText("launch.txt")) {
                    writer.WriteLine("# Add any Everest launch flags here. Lines starting with # are ignored.");
                    writer.WriteLine();
                    writer.WriteLine("# If you're having graphics issues with the FNA version on Windows,");
                    writer.WriteLine("# remove the # from the following line to enable using Direct3D.");
                    writer.WriteLine("#--d3d");
                    writer.WriteLine();
                    writer.WriteLine("# If you've got an Intel GPU, are using the FNA version on Windows and");
                    writer.WriteLine("# are 100% sure that you want to use Intel's possibly broken OpenGL drivers,");
                    writer.WriteLine("# remove the # from the following line to revert to using OpenGL.");
                    writer.WriteLine("#--no-d3d");
                    writer.WriteLine();
                }
            }

            if (args.Contains("--console") && PlatformHelper.Is(MonoMod.Utils.Platform.Windows)) {
                AllocConsole();
            }

            // PlatformHelper is part of MonoMod.
            // Environment.OSVersion.Platform is good enough as well, but Everest consistently uses PlatformHelper.
            // The following is based off of https://github.com/FNA-XNA/FNA/wiki/4:-FNA-and-Windows-API#direct3d-support
            if (PlatformHelper.Is(MonoMod.Utils.Platform.Windows)) {
                bool useD3D = args.Contains("--d3d");

                try {
                    // Keep all usage of System.Management in a separate method.
                    // Member references are resolved as soon as a method is called.
                    // This means that if System.Management cannot be found due to 
                    // f.e. the use of MonoKickstart, this method won't even get as
                    // far as checking the platform.
                    if (DoesGPUHaveBadOpenGLDrivers())
                        useD3D = true;
                } catch {
                    // Silently catch all exceptions: Method and type load errors,
                    // permission / access related exceptions and whatnot.
                }

                if (args.Contains("--no-d3d"))
                    useD3D = false;

                if (useD3D) {
                    Environment.SetEnvironmentVariable("FNA_OPENGL_FORCE_ES3", "1");
                    Environment.SetEnvironmentVariable("SDL_OPENGL_ES_DRIVER", "1");
                }
            }

            if (File.Exists("log.txt"))
                File.Delete("log.txt");

            using (Stream fileStream = new FileStream("log.txt", FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            using (StreamWriter fileWriter = new StreamWriter(fileStream, Console.OutputEncoding))
            using (LogWriter logWriter = new LogWriter {
                STDOUT = Console.Out,
                File = fileWriter
            }) {
                try {
                    Console.SetOut(logWriter);

                    AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

                    try {
                        Everest.ParseArgs(args);
                        orig_Main(args);
                    } catch (Exception e) {
                        CriticalFailureHandler(e);
                        return;
                    } finally {
                        Instance?.Dispose();
                    }

                    Everest.Shutdown();
                } finally {
                    if (logWriter.STDOUT != null) {
                        Console.SetOut(logWriter.STDOUT);
                        logWriter.STDOUT = null;
                    }
                }
            }

        }

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
Probably by force-installing Everest on top of Everest on top of Everest.

IF YOU WANT TO HELP US FIX THIS:
Please join the Celeste Discord server and drag and drop your log.txt into #modding_help.
https://discord.gg/6qjaePQ");

            ErrorLog.Open();
            if (!_CriticalFailureIsUnhandledException)
                Environment.Exit(-1);
        }

        [MonoModIfFlag("OS:NotWindows")]
        [MonoModLinkFrom("System.Boolean Celeste.Celeste::DoesGPUHaveBadOpenGLDrivers()")]
        private static bool StubBadOpenGLDriversCheck() {
            // since we are not on Windows, assume the OpenGL drivers are good.
            return false;
        }

        [MonoModIfFlag("OS:Windows")]
        private static bool DoesGPUHaveBadOpenGLDrivers() {
            bool isBad = false;
            bool checkIntel = true;

            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_VideoController")) {

                // The current machine can have more than one GPU installed.
                // Let's iterate through all GPUs to catch them all, as we can't
                // control which GPU will be used to render the game at runtime.
                foreach (ManagementObject obj in searcher.Get()) {

                    // We can't TryGet, so let's iterate through all available props.
                    foreach (PropertyData prop in obj.Properties) {
                        string key = prop.Name;
                        if (string.IsNullOrEmpty(key))
                            continue;

                        // At least one of those contains "Intel"
                        if (key.Equals("AdapterCompatibility", StringComparison.InvariantCultureIgnoreCase) &&
                            key.Equals("Caption", StringComparison.InvariantCultureIgnoreCase) &&
                            key.Equals("Description", StringComparison.InvariantCultureIgnoreCase) &&
                            key.Equals("VideoProcessor", StringComparison.InvariantCultureIgnoreCase)
                        )
                            continue;

                        // The value can be a non-string and / or null.
                        string value = prop.Value?.ToString();
                        if (string.IsNullOrEmpty(value))
                            continue;

                        if (value.IndexOf("Intel", StringComparison.InvariantCultureIgnoreCase) != -1) {
                            // Good job, this machine has got an Intel GPU and we don't
                            // know if the installed drivers are good enough or not.

                            // Intel chips can be listed multiple times with some important information only present once.
                            if (!checkIntel)
                                break;

                            // Someone reported lag when using ANGLE with an HD Graphics
                            // 4000 and using the latest drivers (2019).
                            // Meanwhile, someone else reported graphics issues with an
                            // HD Graphics 5500 which were fixed by using ANGLE.
                            // I regret my life decisions.
                            if (value == "Intel(R) HD Graphics 4000") {
                                // Don't check this GPU's props any further.
                                isBad = false;
                                break;
                            }

                            // Someone reported the following crash using ANGLE:
                            // Mobile Intel(R) 4 Series Express Chipset Family
                            // NoSuitableGraphicsDeviceException: Could not create GLES window surface
                            // Meanwhile, someone else reported the same crash with a non-mobile variant, yet a missing mountain with OpenGL.
                            if (value == "Mobile Intel(R) 4 Series Express Chipset Family" ||
                                value == "Intel(R) 4 Series Express Chipset Family") {
                                // Don't check this GPU's props any further.
                                isBad = false;
                                checkIntel = false;
                                break;
                            }

                            // Gonna use ANGLE by default on this setup...
                            isBad = true;
                        }
                    }
                }
            }

            return isBad;
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

            if (!firstLoad && Version <= new Version(1, 3, 1, 2)) {
                // Celeste 1.3.1.2 and older runs Directory.Add in PlaybackData.Load
                PlaybackData.Tutorials.Clear();
            }

            orig_LoadContent();

            foreach (EverestModule mod in Everest._Modules)
                mod.LoadContent(firstLoad);
        }

        protected override void OnExiting(object sender, EventArgs args) {
            base.OnExiting(sender, args);
            Everest.Events.Celeste.Exiting();
        }

    }
}
