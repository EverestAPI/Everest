using System;
using System.Diagnostics;
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
                Directory.Delete(Path.Combine(Globals.PathGame, "piton-runtime"), true);
        }

        if (!Directory.Exists(dstPath))
            Directory.CreateDirectory(dstPath);

        foreach (string entrySrc in Directory.GetFileSystemEntries(srcPath)) {
            string entryDst = Path.Combine(dstPath, Path.GetRelativePath(srcPath, entrySrc));

            if (File.Exists(entrySrc)) {
                Logger.LogLine($"Copying {entrySrc} +> {entryDst}");
                File.Copy(entrySrc, entryDst, true);
            } else
                MoveFilesFromUpdate(entrySrc, entryDst);
        }
    }
    
    public static void WaitForGameExit() {
        if (int.TryParse(Environment.GetEnvironmentVariable("EVEREST_UPDATE_CELESTE_PID"), out int celestePid)) {
            try {
                Process celesteProc = Process.GetProcessById(celestePid);
                celesteProc.Kill(false);
                celesteProc.WaitForExit();
            } catch {}
        }

        if (
            (File.Exists(Globals.PathCelesteExe) && !CanReadWrite(Globals.PathCelesteExe)) ||
            (File.Exists(Globals.PathEverestDLL) && !CanReadWrite(Globals.PathEverestDLL))
         ) {
            Logger.LogErr("Celeste not read-writeable - waiting");
            while (!CanReadWrite(Globals.PathCelesteExe))
                Thread.Sleep(5000);
        }
    }
    
    // AFAIK there's no "clean" way to check for any file locks in C#.
    private static bool CanReadWrite(string path) {
        try {
            new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete).Dispose();
            return true;
        } catch {
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