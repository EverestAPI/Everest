using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;

namespace MiniInstaller;

public static class InGameUpdaterHelper {
    public static void MoveFilesFromUpdate(string srcPath = null, string dstPath = null) {
        if (srcPath == null) {
            if (Globals.PathUpdate == null)
                return;

            Logger.LogLine("Moving files from update directory");
            srcPath ??= Globals.PathUpdate;
            dstPath ??= Globals.PathGame;

            // Check if we have a new runtime (=there is a piton-runtime folder both in the game and the update directory)
            if (Directory.Exists(Path.Combine(Globals.PathGame, "piton-runtime")) && Directory.Exists(Path.Combine(Globals.PathUpdate, "piton-runtime")))
                Directory.Delete(Path.Combine(Globals.PathGame, "piton-runtime"), recursive: true);
        }

        if (!Directory.Exists(dstPath))
            Directory.CreateDirectory(dstPath);

        foreach (string entrySrc in Directory.GetFileSystemEntries(srcPath)) {
            string entryDst = Path.Combine(dstPath, Path.GetRelativePath(srcPath, entrySrc));

            if (File.Exists(entrySrc)) {
                Logger.LogLine($"Copying {entrySrc} +> {entryDst}");
                File.Copy(entrySrc, entryDst, overwrite: true);
            } else
                MoveFilesFromUpdate(entrySrc, entryDst);
        }
    }
    
    public static void WaitForGameExit() {
        if (!int.TryParse(Environment.GetEnvironmentVariable("EVEREST_UPDATE_CELESTE_PID"), out int celestePid))
            return;

        try {
            Process celesteProc = Process.GetProcessById(celestePid);
            celesteProc.Kill(false);
            celesteProc.WaitForExit();
        } catch {}
    }

    public static void EnsureGameIsWriteable() {
        MiscUtil.EnsureFileIsWriteable(Globals.PathCelesteExe);
        MiscUtil.EnsureFileIsWriteable(Globals.PathEverestDLL);

        if ((!File.Exists(Globals.PathCelesteExe) || CanReadWrite(Globals.PathCelesteExe)) &&
            (!File.Exists(Globals.PathEverestDLL) || CanReadWrite(Globals.PathEverestDLL)))
            return;

        Logger.LogErr("Celeste is not read-writeable - waiting");

        // let's wait for 1 minute
        const int WaitTimeSeconds = 60;
        const int MaxRetryCount = 12;

        Exception error = null;
        for (int i = 0; i < MaxRetryCount; i++) {
            Thread.Sleep(WaitTimeSeconds * 1000 / MaxRetryCount);

            if (CanReadWrite(Globals.PathCelesteExe, out error) && CanReadWrite(Globals.PathEverestDLL, out error))
                return;
        }

        throw new InvalidOperationException(
            "Celeste is not read-writeable. "
            + "Please ensure the game is not running, its files are not in use and that you have write permissions.",
            error);
    }

    // AFAIK there's no "clean" way to check for any file locks in C#.
    private static bool CanReadWrite(string path)
        => CanReadWrite(path, out _);

    private static bool CanReadWrite(string path, [NotNullWhen(false)] out Exception error) {
        try {
            new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete).Dispose();
            error = null;
            return true;
        } catch (Exception e) {
            error = e;
            return false;
        }
    }
    
    public static void StartGame() {
        Logger.LogLine("Restarting Celeste");

        // Let's not pollute the game with our MonoMod env vars.
        Environment.SetEnvironmentVariable("MONOMOD_DEPDIRS", "");
        Environment.SetEnvironmentVariable("MONOMOD_MODS", "");
        Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "");

        Process game = new Process();
        // If the game was installed via Steam, it should restart in a Steam context on its own.
        if (Globals.Platform != Globals.InstallPlatform.Windows) {
            // The Linux and macOS version apphosts don't end in ".exe"
            game.StartInfo.FileName = Path.ChangeExtension(Globals.PathCelesteExe, null);
        } else {
            game.StartInfo.FileName = Globals.PathCelesteExe;
        }
        game.StartInfo.WorkingDirectory = Globals.PathGame;
        game.Start();
    }
}
