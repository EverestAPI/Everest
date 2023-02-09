using Celeste.Mod.Helpers;
using Monocle;
using MonoMod;
using Steamworks;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace Celeste.Mod {
    /// <summary>
    /// RUN AWAY. TURN AROUND. GO TO CELESTE'S MAIN FUNCTION INSTEAD.
    /// </summary>
    internal static class BOOT {

        // TODO Fast restarts through AssemblyLoadContexts
        // TODO Restart into vanilla

        [MakeEntryPoint]
        private static void Main(string[] args) {
            try {
                CultureInfo originalCurrentThreadCulture = Thread.CurrentThread.CurrentCulture;
                CultureInfo originalCurrentThreadUICulture = Thread.CurrentThread.CurrentUICulture;

                // 0.1 parses into 1 in regions using ,
                // This also somehow sets the exception message language to English.
                CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

                string everestPath = typeof(Celeste).Assembly.Location;

                // Launching Celeste.exe from a shortcut can sometimes set cwd to System32 on Windows.
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    Environment.CurrentDirectory = Path.GetDirectoryName(everestPath);

                try {
                    if (RestartViaLauncher())
                        return;
                } catch {
                }

                // Required for native libs to be picked up on Linux
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                    string[] ldPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH")?.Split(":") ?? Array.Empty<string>();
                    string execLdPath = Path.GetFullPath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
                    if (!ldPath.Any(path => Path.GetFullPath(path) == execLdPath)) {
                        Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", $"{execLdPath}:{Environment.GetEnvironmentVariable("LD_LIBRARY_PATH")}");
                        Console.WriteLine($"Restarting with LD_LIBRARY_PATH=\"{Environment.GetEnvironmentVariable("LD_LIBRARY_PATH")}\"...");

                        Process proc = StartCelesteProcess();
                        proc.WaitForExit();
                        Environment.Exit(proc.ExitCode);
                    }
                }

                patch_Celeste.Main(args);

                if (AppDomain.CurrentDomain.GetData("EverestRestart") as bool? ?? false) {
                    //TODO Try to reimplement quick everest restarts
                    StartCelesteProcess();
                    goto Exit;
                } else if (Everest.RestartVanilla) {
                    //TODO
                    throw new NotImplementedException();
                }
            } catch (Exception e) {
                LogError("BOOT-CRITICAL", e);
                goto Exit;
            }


            // Needed because certain graphics drivers and native libs like to hang around for no reason.
            // Vanilla does the same on macOS and Linux, but NVIDIA on Linux likes to waste time in DrvValidateVersion.
            Exit:
            Console.WriteLine("Exiting Celeste process");
            Environment.Exit(0);
        }

        public static void LogError(string tag, Exception e) {
            e.LogDetailed(tag);
            try {
                ErrorLog.Write(e.ToString());
                ErrorLog.Open();
            } catch { }
        }

        [MonoModIgnore]
        private static extern bool RestartViaLauncher();

        [MonoModIfFlag("Steamworks")]
        [MonoModPatch("RestartViaLauncher")]
        [MonoModReplace]
        private static bool RestartViaSteam() {
            return SteamAPI.RestartAppIfNecessary(new AppId_t(504230));
        }

        [MonoModIfFlag("NoLauncher")]
        [MonoModPatch("RestartViaLauncher")]
        [MonoModReplace]
        private static bool RestartViaNoLauncher() {
            return false;
        }

        // Last resort full restart in case we're unable to unload the AppDomain while quick-restarting.
        // This is also used by Everest.SlowFullRestart
        public static Process StartCelesteProcess() {
            string path = Path.GetDirectoryName(typeof(Celeste).Assembly.Location);

            Process game = new Process();

            // Unix-likes use the wrapper script
            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX) {
                game.StartInfo.FileName = Path.Combine(path, "Celeste");
                // 1.3.3.0 splits Celeste into two, so to speak.
                if (!File.Exists(game.StartInfo.FileName) && Path.GetFileName(path) == "Resources")
                    game.StartInfo.FileName = Path.Combine(Path.GetDirectoryName(path), "MacOS", "Celeste");
            } else {
                game.StartInfo.FileName = Path.Combine(path, "Celeste.exe");
            }

            game.StartInfo.WorkingDirectory = path;

            Regex escapeArg = new Regex(@"(\\+)$");
            game.StartInfo.Arguments = string.Join(" ", Environment.GetCommandLineArgs().Select(s => "\"" + escapeArg.Replace(s, @"$1$1") + "\""));

            game.Start();
            return game;
        }

    }
}
