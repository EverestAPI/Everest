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

        [MakeEntryPoint]
        private static void Main(string[] args) {
            try {
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

                // Required for native libs to be picked up on Linux / MacOS
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    string execLdPath = Path.GetFullPath(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? 
                        AppContext.BaseDirectory : 
                        Path.Combine(AppContext.BaseDirectory, "..", "MacOS", "osx")
                    );

                    string[] ldPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH")?.Split(":") ?? Array.Empty<string>();
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
                    // Restart the original process
                    // This is as fast as the old "fast restarts" were
                    StartCelesteProcess();
                    goto Exit;
                } else if (Everest.RestartVanilla) {
                    // Start the vanilla process
                    StartCelesteProcess(Path.Combine(AppContext.BaseDirectory, "orig"));
                    goto Exit;
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

        public static Process StartCelesteProcess(string gameDir = null) {
            gameDir ??= AppContext.BaseDirectory;

            Process game = new Process();

            game.StartInfo.FileName = Path.Combine(gameDir,
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Celeste.exe" :
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux)   ? "Celeste" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX)     ? "Celeste" :
                throw new Exception("Unknown OS platform")
            );
            game.StartInfo.WorkingDirectory = gameDir;

            Regex escapeArg = new Regex(@"(\\+)$");
            game.StartInfo.Arguments = string.Join(" ", Environment.GetCommandLineArgs().Select(s => "\"" + escapeArg.Replace(s, @"$1$1") + "\""));

            game.Start();
            return game;
        }

    }
}
