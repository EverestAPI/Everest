using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MiniInstaller;

public static class WindowsElevationRequest {

    [SupportedOSPlatform("windows")]
    public static bool HandleWindowsElevationProcedure(Exception e) {
        const uint ERROR_ACCESS_DENIED = 0x80070005U;
        const uint ERROR_PRIVILEGE_NOT_HELD = 0x80070522U;

        const uint ERROR_INVALID_FUNCTION = 0x80070001U;

        switch (unchecked((uint) e.HResult)) {
            case ERROR_ACCESS_DENIED or ERROR_PRIVILEGE_NOT_HELD:
                Logger.LogLine("Failed to create backup symlinks due to missing privilege or access denial - asking user if they want to retry with elevation");
                // On Windows, offer to try again with elevation
                if (!CreateBackupSymlinksWithElevation()) {
                    return false;
                }
                break;
            case ERROR_INVALID_FUNCTION:
                Logger.LogLine("Failed to create backup symlinks due to invalid function - warning user");
                if (!WarnAboutBackupSymlinkFilesystem()) {
                    return false;
                }
                break;
            default:
                return false;
        }

        return true;
    }
    
    [SupportedOSPlatform("windows")]
    private static bool CreateBackupSymlinksWithElevation() {
        switch (
            MessageBox(0, """
            The installer requires administrator privileges during the first installation to link the vanilla installation to the modded one. 
            This is required to share save data with the "restart into vanilla" feature.
            If denied, installation will continue, but saves will NOT be shared between vanilla and Everest.

            Proceed with administrator privileges?
            """.Trim(), "Everest Installation Elevation Request", 0x00000003U | 0x00000040U | 0x00010000U) // MB_YESNOCANCEL | MB_ICONINFORMATION | MB_SETFOREGROUND
        ) {
            case 2: // IDCANCEL
                Logger.LogLine("User cancelled installation - rethrowing original error");
                return false;
            case 6: // IDYES
                Logger.LogLine("User accepted elevation request - starting elevated process");

                //Create symlinks with elevation
                retry:;
                try {
                    ProcessStartInfo startInfo = new ProcessStartInfo() {
                        FileName = Environment.ProcessPath ?? throw new Exception("No process path available"),
                        Verb = "RunAs",
                        UseShellExecute = true
                    };
                    foreach (string arg in Environment.GetCommandLineArgs()[1..])
                        startInfo.ArgumentList.Add(arg);

                    startInfo.ArgumentList.Add($"{nameof(CreateBackupSymlinksWithElevation)}_PostElevationRequest");
                    startInfo.ArgumentList.Add(Globals.PathGame);
                    startInfo.ArgumentList.Add(Globals.PathOrig);

                    Process elevatedProc = Process.Start(startInfo);
                    elevatedProc.WaitForExit();
                    if (elevatedProc.ExitCode == 0) {
                        Logger.LogLine("Succesfully created backup symlinks with elevation");
                        break;
                    }
                } catch (Win32Exception e) {
                    if (e.NativeErrorCode == 1223 || unchecked((uint) e.HResult) == 0x800704c7) // ERROR_CANCELLED
                        Logger.LogLine("User cancelled elevation request");
                    else
                        throw;
                }

                //Failed to create symlinks
                Logger.LogLine("Failed to create backup symlinks with elevation - offering user to retry");

                switch (
                    MessageBox(0, """
                    Failed to link the vanilla installation to the modded one with elevated privileges.
                    This could be caused by declining the elevation request.
                    Without elevation, installation will proceed normally, but saves will NOT be shared between vanilla and Everest.

                    Would you like to retry?
                    """.Trim(), "Everest Installation Error", 0x00000006U | 0x00000010U | 0x00010000U) // MB_CANCELTRYCONTINUE | MB_ICONERROR | MB_SETFOREGROUND
                ) {
                    case 2: // IDCANCEL
                        Logger.LogLine("User cancelled installation - rethrowing original error");
                        return false;
                    case 10: // IDTRYAGAIN
                        Logger.LogLine("Retrying elevated symlink creation");
                        goto retry;
                    case 11: // IDCONTINUE
                        Logger.LogLine("User chose to contine installation - running fallback logic");
                        BackUp.CreateBackupSymlinksFallback();
                        break;
                    case 0: throw new Win32Exception();
                }

                break;
            case 7: // IDNO
                // Run fallback logic
                Logger.LogLine("User denied elevation request - running fallback logic");
                BackUp.CreateBackupSymlinksFallback();
                break;
            case 0: throw new Win32Exception();
        }

        return true;
    }
    
    [SupportedOSPlatform("windows")]
    private static bool WarnAboutBackupSymlinkFilesystem() {
        switch (
            MessageBox(0, """
            The installer failed to link the vanilla installation to the modded one due to missing support by your filesystem.
            Installation can continue, but saves will NOT be shared between vanilla and Everest.
            To fix this issue, install vanilla Celeste on an NTFS partition (this generally means a hard drive/SSD instead of an SD card or flash drive) and repeat the installation there.
            """.Trim(), "Everest Installation Filesystem Warning", 0x00000001U | 0x00000030U | 0x00010000U) // MB_OKCANCEL | MB_ICONWARNING | MB_SETFOREGROUND)
        ) {
            case 2: // IDCANCEL
                Logger.LogLine("User cancelled installation - rethrowing original error");
                return false;
            case 1: // IDOK
                Logger.LogLine("Continuing installation on non-NTFS filesystem - running fallback logic");
                BackUp.CreateBackupSymlinksFallback();
                break;
            case 0:
                throw new Win32Exception();
        }
        return true;
    }

    [SupportedOSPlatform("windows")]
    public static bool HandlePostElevationBackup(string[] args) {
        // Handle creating backup symlinks after obtaining elevation
        if (args.Length <= 0 || args[0] != $"{nameof(CreateBackupSymlinksWithElevation)}_PostElevationRequest") return false;
        Globals.PathGame = args[1];
        Globals.PathOrig = args[2];
        BackUp.CreateBackupSymlinks();
        return true;
    }
    
    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll", SetLastError=true, CharSet=CharSet.Auto)]
    static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);
    
}