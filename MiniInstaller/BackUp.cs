using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace MiniInstaller;

public static class BackUp {
    public static void Backup() {
        // Backup / restore the original game files we're going to modify.
        // TODO: Maybe invalidate the orig dir when the backed up version < installed version?
        if (!Directory.Exists(Globals.PathOrig)) {
            Logger.LogLine("Creating backup orig directory");
            Directory.CreateDirectory(Globals.PathOrig);
        }

        // Backup the game executable
        Backup(Globals.PathCelesteExe);

        // Backup game dependencies
        BackupPEDeps(Path.Combine(Globals.PathOrig, Path.GetRelativePath(Globals.PathGame, Globals.PathCelesteExe)), Globals.PathGame);

        // Backup all system libraries explicitly, as we'll delete those
        foreach (string file in Directory.GetFiles(Globals.PathGame)) {
            if (MiscUtil.IsSystemLibrary(file))
                Backup(file);
        }

        // Backup MonoKickstart executable / config (for Linux + MacOS)
        Backup(Path.Combine(Globals.PathOSXExecDir ?? Globals.PathGame, "Celeste"));
        Backup(Path.Combine(Globals.PathGame, "Celeste.bin.x86"));
        Backup(Path.Combine(Globals.PathGame, "Celeste.bin.x86_64"));
        Backup(Path.Combine(Globals.PathGame, "monoconfig"));
        Backup(Path.Combine(Globals.PathGame, "monomachineconfig"));
        Backup(Path.Combine(Globals.PathGame, "FNA.dll.config"));

        // Backup native libraries
        foreach (string libName in Globals.WindowsNativeLibFileNames)
            Backup(Path.Combine(Globals.PathGame, libName));
        Backup(Path.Combine(Globals.PathGame, "lib"));
        Backup(Path.Combine(Globals.PathGame, "lib64"));
        if (Globals.PathOSXExecDir != null)
            Backup(Path.Combine(Globals.PathOSXExecDir, "osx"));

        // Backup misc files
        Backup(Globals.PathCelesteExe + ".config");
        Backup(Path.Combine(Globals.PathGame, "gamecontrollerdb.txt"));

        // Apply patch vanilla libraries
        string patchLibsDir = Path.Combine(Globals.PathEverestLib, "lib-vanilla");
        if (Directory.Exists(patchLibsDir)) {
            static void ApplyVanillaPatchLibs(string patchLibsDir, string targetDir) {
                foreach (string src in Directory.GetFileSystemEntries(patchLibsDir)) {
                    string dst = Path.Combine(targetDir, Path.GetRelativePath(patchLibsDir, src));
                    if (File.Exists(src)) {
                        if (File.Exists(dst))
                            File.Delete(dst);
                        File.Move(src, dst);
                    } else if (Directory.Exists(src)) {
                        Directory.CreateDirectory(dst);
                        ApplyVanillaPatchLibs(src, dst);
                    }
                }
            }

            Logger.LogLine("Applying patch vanilla libraries");
            ApplyVanillaPatchLibs(patchLibsDir, Globals.PathOrig);
            Directory.Delete(patchLibsDir, true);
        }

        //Create symlinks
        try {
            CreateBackupSymlinks();
        } catch (Exception e) {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                throw;
            }

            if (!WindowsElevationRequest.HandleWindowsElevationProcedure(e))
                throw;
        }
    }

    public static void CreateBackupSymlinks() {
        if (!Directory.Exists(Path.Combine(Globals.PathOrig, "Content")))
            Directory.CreateSymbolicLink(Path.Combine(Globals.PathOrig, "Content"), Path.GetRelativePath(Globals.PathOrig, Path.Combine(Globals.PathGame, "Content")));

        if (Globals.Platform == Globals.InstallPlatform.Windows && !Directory.Exists(Path.Combine(Globals.PathOrig, "Saves"))) {
            Directory.CreateDirectory(Path.Combine(Globals.PathGame, "Saves"));
            Directory.CreateSymbolicLink(Path.Combine(Globals.PathOrig, "Saves"), Path.GetRelativePath(Globals.PathOrig, Path.Combine(Globals.PathGame, "Saves")));
        }
    }

    public static void CreateBackupSymlinksFallback() {
        if (!Directory.Exists(Path.Combine(Globals.PathOrig, "Content"))) {
            static void CopyDirectory(string src, string dst) {
                Directory.CreateDirectory(dst);

                foreach (string file in Directory.GetFiles(src))
                    File.Copy(file, Path.Combine(dst, Path.GetRelativePath(src, file)));

                foreach (string dir in Directory.GetDirectories(src))
                    CopyDirectory(dir, Path.Combine(dst, Path.GetRelativePath(src, dir)));
            }
            CopyDirectory(Path.Combine(Globals.PathGame, "Content"), Path.Combine(Globals.PathOrig, "Content"));
        }

        // We can't have a fallback for the saves folder symlink
    }

    private static void BackupPEDeps(string path, string depFolder, HashSet<string> backedUpDeps = null) {
        backedUpDeps ??= new HashSet<string>() { path };

        foreach (string dep in MiscUtil.GetPEAssemblyReferences(path).Keys) {
            string asmRefPath = Path.Combine(depFolder, $"{dep}.dll");
            if (!File.Exists(asmRefPath) || backedUpDeps.Contains(asmRefPath))
                continue;

            backedUpDeps.Add(asmRefPath);
            if (File.Exists(Path.Combine(Globals.PathOrig, $"{dep}.dll")))
                continue;

            Backup(asmRefPath);
            BackupPEDeps(asmRefPath, depFolder, backedUpDeps);
        }
    }

    private static void Backup(string from, string backupDst = null) {
        string to = Path.Combine(backupDst ?? Globals.PathOrig, Path.GetFileName(from));
        if(Directory.Exists(from)) {
            if (!Directory.Exists(to))
                Directory.CreateDirectory(to);

            foreach (string entry in Directory.GetFileSystemEntries(from))
                Backup(entry, to);
        } else if(File.Exists(from)) {
            if (File.Exists(from) && !File.Exists(to)) {
                Logger.LogLine($"Backing up {from} => {to}");
                File.Copy(from, to);
            }
        }
    }
}