using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static class EverestSplashHandler {
        // Will be null when the splash is not running
        private static Process splashProcess;
        private static NamedPipeServerStream splashPipeServerStream;
        private static Task splashPipeServerStreamConnection;
        private static readonly object splashPipeLock = new();
        public static bool SplashRan { get; private set; }
        public static void RunSplash(string targetRenderer = "") {

            int currentPid = Environment.ProcessId;

            try {
                lock (splashPipeLock) {
                    splashPipeServerStream = new NamedPipeServerStream("EverestSplash" + currentPid);
                    splashPipeServerStreamConnection = splashPipeServerStream.WaitForConnectionAsync();
                }
            } catch (IOException e) { // Server address is in use
                Logger.Log(LogLevel.Error, "EverestSplash", "Could not start up splash server!, skipping splash");
                Logger.LogDetailed(e);
                splashPipeServerStreamConnection = null;
                if (splashPipeServerStream != null) {
                    if (splashPipeServerStream.IsConnected)
                        splashPipeServerStream.Disconnect();

                    splashPipeServerStream.Dispose();
                    splashPipeServerStream = null;
                }
            }

            // Only proceed if the server was successful
            if (splashPipeServerStream == null) return;

            try {
                splashProcess = new Process {
                    StartInfo = new ProcessStartInfo(Path.Combine(".", "EverestSplash",
                            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                ? RuntimeInformation.OSArchitecture == Architecture.X64
                                    ? "EverestSplash-win64.exe"
                                    : "EverestSplash-win.exe"
                                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                                    ? "EverestSplash-linux"
                                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                                        ? "EverestSplash-osx"
                                        : throw new Exception("Unknown OS platform")
                        ),
                        (targetRenderer != "" ? "--graphics " + targetRenderer : "") +
                        " " +
                        "--server-postfix " + currentPid),
                };

                // Required for the logger to pick up the splash stdout as well
                splashProcess.StartInfo.RedirectStandardOutput = true;
                splashProcess.StartInfo.RedirectStandardError = true;
                splashProcess.OutputDataReceived += (_, data) => {
                    if (data.Data == null || data.Data.Trim().TrimEnd('\n', '\r') == "")
                        return; // Sometimes we may receive nulls or just blank lines, skip those
                    Logger.Log(LogLevel.Info, "EverestSplash", data.Data);
                };
                splashProcess.ErrorDataReceived += (_, data) => {
                    if (data.Data == null || data.Data.Trim().TrimEnd('\n', '\r') == "") return;
                    Logger.Log(LogLevel.Error, "EverestSplash", data.Data);
                };
                
                // Dirty fix: really make sure the splash is executable on *nix
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    Process chmod = Process.Start(new ProcessStartInfo("chmod", $"u+x \"{splashProcess.StartInfo.FileName}\""));
                    if (chmod == null) Logger.Log(LogLevel.Error, "EverestSplash", "Could not chmod splash!");
                    chmod?.WaitForExit();
                    if (chmod?.ExitCode != 0)
                        Logger.Log(LogLevel.Error, "EverestSplash", "Chmod failed for the splash!");
                }

                splashProcess.Start();
                splashProcess.BeginOutputReadLine(); // This is required for the event to even be sent
                splashProcess.BeginErrorReadLine();
                SplashRan = true;
            } catch (Exception e) {
                Logger.Log(LogLevel.Error, "EverestSplash", "Starting splash failed!");
                Logger.LogDetailed(e);
                // Destroy the server asap
                if (splashPipeServerStream.IsConnected)
                    splashPipeServerStream.Disconnect();
                splashPipeServerStream.Dispose();
                splashPipeServerStream = null;
                splashPipeServerStreamConnection = null;
                splashProcess = null;
            }
        }

        private static int loadedMods = 0, totalMods = 0;

        public static void SetSplashLoadingModCount(int modCount) {
            totalMods = modCount;
        }

        public static void IncreaseTotalModCount(int amount) {
            totalMods += amount;
        }

        public static void IncreaseLoadedModCount(string latestLoadedMod) {
            loadedMods++;
            SendMessageToSplash("#progress" + loadedMods + ";" + totalMods + ";" + latestLoadedMod);
        }

        public static void AllModsLoaded() {
            loadedMods = totalMods;
            SendMessageToSplash("#finish" + totalMods + ";" + "Almost done...");
        }

        private static Task lastFlush;
        private static void SendMessageToSplash(string message) {
            lock (splashPipeLock) {
                if (splashPipeServerStream == null)
                    return; // If the splash never ran, no-op
                if (!splashPipeServerStreamConnection.IsCompleted || !splashPipeServerStream.IsConnected)
                    return; // If the splash never connected or its no longer alive, no-op
                if (lastFlush != null && !lastFlush.IsCompletedSuccessfully)
                    return; // If the last flush failed or did not complete in time, drop the messages
                try {
                    StreamWriter sw = new(splashPipeServerStream);
                    sw.WriteLine(message);
                    lastFlush = sw.FlushAsync(); // Windows pipes have a tendency to deadlock writes if the other end doesn't read immediately
                    lastFlush.Wait(1000);
                } catch (Exception e) {
                    Logger.Log(LogLevel.Error, "EverestSplash", "Could not send data to splash!");
                    Logger.LogDetailed(e);
                }
            }
        }

        public static void StopSplash() {
            lock (splashPipeLock) {
                // If the user has not specified this and the splash ran then do request focus
                if (!SplashRan && Environment.GetEnvironmentVariable("EVEREST_SKIP_REQUEST_FOCUS_AFTER_SPLASH") == null) {
                    Environment.SetEnvironmentVariable("EVEREST_SKIP_REQUEST_FOCUS_AFTER_SPLASH", "1");
                }
                if (splashPipeServerStream == null) return; // If the splash never ran, no-op
                if (!splashPipeServerStreamConnection.IsCompleted || !lastFlush.IsCompletedSuccessfully) {
                    Logger.Log(LogLevel.Error, "EverestSplash", "Could not connect to splash");
                    if (!splashProcess.HasExited) { // if it hangs up, just kill it
                        splashProcess.Kill();
                        Logger.Log(LogLevel.Error, "EverestSplash", "Splash was still alive. Killed splash!");
                    }
                    return;
                }

                try {
                    StreamWriter sw = new(splashPipeServerStream);
                    sw.WriteLine("#stop");
                    sw.Flush();
                    Thread splashFeedbackThread = new(() => {
                        try {
                            // `splashPipeServerStream` is intentionally disposed on outer stream, it's the easiest way to kill this thread
                            StreamReader sr = new(splashPipeServerStream);
                            // yes, this, inevitably, slows down the everest boot process, but see EverestSplashWindow.FeedBack
                            // for more info
                            sr.ReadLine();
                        } catch (Exception e) {
                            Logger.Log(LogLevel.Error, "EverestSplash", "Could not read line!");
                            Logger.LogDetailed(e);
                        }
                    });
                    splashFeedbackThread.Start();
                    bool stopSuccessful = splashFeedbackThread.Join(TimeSpan.FromSeconds(10)); // Big enough timeout for any modern computer
                    if (!stopSuccessful) {
                        Logger.Log(LogLevel.Error, "EverestSplash", "Timeout!, splash did not respond, continuing...");
                        if (!splashProcess.HasExited) { // if it hangs up, just kill it
                            splashProcess.Kill();
                            Logger.Log(LogLevel.Error, "EverestSplash", "Splash was still alive. Killed splash!");
                        }
                    }
                    // Destroy the server asap for any future boots
                    if (splashPipeServerStream.IsConnected)
                        splashPipeServerStream.Disconnect();
                    splashPipeServerStream.Dispose();
                    splashPipeServerStream = null;
                    splashPipeServerStreamConnection = null;
                    splashProcess = null;
                } catch (Exception e) {
                    Logger.Log(LogLevel.Error, "EverestSplash", "Could not stop splash!");
                    Logger.LogDetailed(e);
                }
            }
        }

    }
}
