using System;
using System.Collections.ObjectModel;
using System.IO;

namespace MiniInstaller;

public static class Globals {
    public static readonly ReadOnlyCollection<string> WindowsNativeLibFileNames = Array.AsReadOnly(new string[] {
        "fmod.dll", "fmodstudio.dll", "CSteamworks.dll", "steam_api.dll", "FNA3D.dll", "SDL2.dll"
    });
    
    public static readonly ReadOnlyCollection<string> EverestSystemLibs = Array.AsReadOnly(new string[] {
        "System.Drawing.Common.dll", "System.Security.Permissions.dll", "System.Windows.Extensions.dll"
    });
    
    public static string PathUpdate;
    public static string PathGame;
    public static string PathOSXExecDir;
    public static string PathCelesteExe;
    public static string PathEverestDLL;
    public static string PathEverestLib;
    public static string PathOrig;
    public static string PathLog;
    public static string PathTmp;
    public static bool SetupPaths() {
        PathGame = Directory.GetCurrentDirectory();
        Console.WriteLine(PathGame);

        if (Path.GetFileName(PathGame) == "everest-update" && (
                File.Exists(Path.Combine(Path.GetDirectoryName(PathGame), "Celeste.exe")) ||
                File.Exists(Path.Combine(Path.GetDirectoryName(PathGame), "Celeste.dll"))
            )) {
            // We're updating Everest via the in-game installler.
            PathUpdate = PathGame;
            PathGame = Path.GetDirectoryName(PathUpdate);
        }

        PathCelesteExe = Path.Combine(PathGame, "Celeste.exe");
        if (!File.Exists(PathCelesteExe) && !File.Exists(Path.ChangeExtension(PathCelesteExe, ".dll"))) {
            Logger.LogErr("Celeste.exe not found!");
            Logger.LogErr("Did you extract the .zip into the same place as Celeste?");
            return false;
        }

        // Here lies a reminder that patching into Everest.exe only caused confusion and issues.
        // RIP Everest.exe 2019 - 2020
        // PathEverestExe = PathCelesteExe;
        PathEverestDLL = Path.ChangeExtension(PathCelesteExe, ".dll");
        PathEverestLib = Path.Combine(Path.GetDirectoryName(PathCelesteExe), "everest-lib");

        PathOrig = Path.Combine(PathGame, "orig");
        PathLog = Path.Combine(PathGame, "miniinstaller-log.txt");

        if (!Directory.Exists(Path.Combine(PathGame, "Mods"))) {
            Logger.LogLine("Creating Mods directory");
            Directory.CreateDirectory(Path.Combine(PathGame, "Mods"));
        }

        // Can't check for platform as some people could be running MiniInstaller via wine.
        if (PathGame.Replace(Path.DirectorySeparatorChar, '/').Trim('/').EndsWith(".app/Contents/Resources")) {
            PathOSXExecDir = Path.Combine(Path.GetDirectoryName(PathGame), "MacOS");
            if (!Directory.Exists(PathOSXExecDir)) PathOSXExecDir = null;
        }

        PathTmp = Directory.CreateTempSubdirectory("Everest_MiniInstaller").FullName;

        return true;
    }

    public enum InstallPlatform {
        Windows, Linux, MacOS
    }

    public static InstallPlatform Platform;
    public static void DetermineInstallPlatform() {
        if (Environment.GetEnvironmentVariable("MINIINSTALLER_PLATFORM") is string platformEnv && !string.IsNullOrEmpty(platformEnv))
            Platform = Enum.Parse<InstallPlatform>(platformEnv, true);
        else {
            // We can't use RuntimeInformation because of wine
            if (PathOSXExecDir != null)
                Platform = InstallPlatform.MacOS;
            else if (File.Exists(Path.ChangeExtension(PathCelesteExe, null)))
                Platform = InstallPlatform.Linux;
            else
                Platform = InstallPlatform.Windows;
        }

        Logger.LogLine($"Determined install platform: {Platform}");
    }
}