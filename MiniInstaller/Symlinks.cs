using MiniInstaller.SDL2;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

namespace MiniInstaller;

public static class Symlinks {
    public static bool Supported;
    public static bool NeedElevation;
    public static bool UserConfirmedInstallationWithoutSymlinks;

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static void DetermineSymlinkSupport() {
        if (UserConfirmedInstallationWithoutSymlinks)
            // --no-symlinks was passed in cmdline args
            return;

        string symlinkPath = Path.Join(Globals.PathGame, ".symlink-probe");
        try {
            File.CreateSymbolicLink(symlinkPath, Globals.PathCelesteExe);
            File.Delete(symlinkPath);
            Supported = true;
        } catch (Exception e) {
            Supported = false;

            // return gracefully on known exceptions
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                // on Windows, we get an IOException whose HRESULT is ERROR_INVALID_FUNCTION if the target filesystem does not support symlinks.
                if (e is not IOException ioe)
                    throw;

                const int ERROR_INVALID_FUNCTION = unchecked((int) 0x80070001);
                if (ioe.HResult is ERROR_INVALID_FUNCTION)
                    return;

                // we might also get an ERROR_ACCESS_DENIED or ERROR_PRIVILEGE_NOT_HELD because for some reason Windows needs elevation
                // to make a symlink, even though Unix systems don't?
                // if we're here, this means symlinks are supported; we just don't have permission.
                const int ERROR_ACCESS_DENIED = unchecked((int) 0x80070005);
                const int ERROR_PRIVILEGE_NOT_HELD = unchecked((int) 0x80070522);
                if (ioe.HResult is ERROR_ACCESS_DENIED or ERROR_PRIVILEGE_NOT_HELD) {
                    Supported = true;
                    NeedElevation = true;
                    return;
                }
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                // on Unix systems, the symlink syscall returns EPERM if the target filesystem does not support symlinks - see symlink(2).
                // however, EACCES, EBADF and EPERM all get converted to UnauthorizedAccessException with an inner IOException whose HRESULT
                // is the actual error code. we only expect EPERM here.
                // https://github.com/dotnet/runtime/blob/18fd75c847399745c43b5970fec840ba71064e80/src/libraries/Common/src/Interop/Unix/Interop.IOErrors.cs#L136-L142

                // ReSharper disable once InconsistentNaming
                const int EPERM = 1;
                if (e is UnauthorizedAccessException { InnerException: IOException { HResult: EPERM } })
                    return;
            }

            throw;
        }
    }

    public static bool ContinueInstallationWithoutSymlinks() {
        if (UserConfirmedInstallationWithoutSymlinks)
            return true;

        string targetFileSystem = new DriveInfo(Globals.PathOrig).DriveFormat;

        // can't use RuntimeInformation as people may be running under wine
        string suggestedFileSystem = Globals.Platform switch {
            Globals.InstallPlatform.Windows => "an NTFS",
            Globals.InstallPlatform.Linux => "an ext4",
            Globals.InstallPlatform.MacOS => "an APFS",
            _ => "another", // lmao
        };

        Logger.LogLine($"Target filesystem ({targetFileSystem}) does not support symlinks - asking user whether to continue installation");
        SDL.SDL_MessageBoxData messageBox = new SDL.SDL_MessageBoxData {
            flags = SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_WARNING,
            title = "Everest Installation Filesystem Warning",
            message = $"""
                The installer cannot link the vanilla installation to the modded one due to missing support from your filesystem, which is {targetFileSystem}.
                Installation can continue, but vanilla and Everest saves will be separated.

                To fix this issue, install vanilla Celeste on {suggestedFileSystem} partition and repeat the installation there.
                This generally means installing to a hard drive/SSD instead of an SD card or flash drive.

                Do you want to continue installing Everest?
                """,
            buttons = new[] {
                new SDL.SDL_MessageBoxButtonData {
                    buttonid = 0,
                    flags = SDL.SDL_MessageBoxButtonFlags.SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT,
                    text = "No",
                },
                new SDL.SDL_MessageBoxButtonData {
                    buttonid = 1,
                    flags = SDL.SDL_MessageBoxButtonFlags.SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT,
                    text = "Yes",
                },
            },
            numbuttons = 2,
            colorScheme = null,
        };

        if (SDL.SDL_ShowMessageBox(ref messageBox, out int buttonId) < 0)
            throw new InvalidOperationException($"{nameof(SDL.SDL_ShowMessageBox)} failed: {SDL.SDL_GetError()}");

        if (buttonId == 0) {
            Logger.LogLine("User cancelled installation - exiting");
            return false;
        }

        Logger.LogLine($"User proceeded with installation on {targetFileSystem} - continuing");
        UserConfirmedInstallationWithoutSymlinks = true;
        return true;
    }

    public enum ElevationRequestResult {
        Accepted,
        Rejected,
        InstallationCancelled,
    }

    public static ElevationRequestResult CreateBackupSymlinksWithElevation() {
        SDL.SDL_MessageBoxData messageBox = new SDL.SDL_MessageBoxData {
            flags = SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_INFORMATION,
            title = "Everest Installation Elevation Request",
            message = """
                The installer requires administrator privileges during the first installation to link the vanilla installation to the modded one.
                If denied, installation will continue, but vanilla and Everest saves will be separated.

                Do you want to proceed with administrator privileges?
                """,
            buttons = new[] {
                new SDL.SDL_MessageBoxButtonData {
                    buttonid = 0,
                    flags = SDL.SDL_MessageBoxButtonFlags.SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT,
                    text = "Cancel",
                },
                new SDL.SDL_MessageBoxButtonData {
                    buttonid = 1,
                    flags = 0,
                    text = "No",
                },
                new SDL.SDL_MessageBoxButtonData {
                    buttonid = 2,
                    flags = SDL.SDL_MessageBoxButtonFlags.SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT,
                    text = "Yes",
                },
            },
            numbuttons = 3,
            colorScheme = null,
        };

        if (SDL.SDL_ShowMessageBox(ref messageBox, out int buttonId) < 0)
            throw new InvalidOperationException($"{nameof(SDL.SDL_ShowMessageBox)} failed: {SDL.SDL_GetError()}");

        switch (buttonId) {
            case 0:
                Logger.LogLine("User cancelled installation - exiting");
                return ElevationRequestResult.InstallationCancelled;
            case 1:
                // Run fallback logic
                Logger.LogLine("User denied elevation request - running fallback logic");
                return ElevationRequestResult.Rejected;
            case 2:
                Logger.LogLine("User accepted elevation request - starting elevated process");

                //Create symlinks with elevation
                retry:;
                try {
                    ProcessStartInfo startInfo = new ProcessStartInfo {
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
                        return ElevationRequestResult.Accepted;
                    }
                } catch (Win32Exception e) {
                    const int ERROR_CANCELLED = unchecked((int) 0x800704c7);
                    if (e.NativeErrorCode != 1223 && e.HResult != ERROR_CANCELLED)
                        throw;

                    Logger.LogLine("User cancelled elevation request");
                }

                //Failed to create symlinks
                Logger.LogLine("Failed to create backup symlinks with elevation - offering user to retry");
                SDL.SDL_MessageBoxData retryMessageBox = new SDL.SDL_MessageBoxData {
                    flags = SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR,
                    title = "Everest Installation Elevation Error",
                    message = """
                        Failed to link the vanilla installation to the modded one with administrator privileges.
                        This could be caused by declining the elevation request.

                        Installation can continue without elevation, but vanilla and Everest saves will be separated.

                        Would you like to retry?
                        """,
                    buttons = new[] {
                        new SDL.SDL_MessageBoxButtonData {
                            buttonid = 0,
                            flags = SDL.SDL_MessageBoxButtonFlags.SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT,
                            text = "Cancel",
                        },
                        new SDL.SDL_MessageBoxButtonData {
                            buttonid = 1,
                            flags = 0,
                            text = "No",
                        },
                        new SDL.SDL_MessageBoxButtonData {
                            buttonid = 2,
                            flags = SDL.SDL_MessageBoxButtonFlags.SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT,
                            text = "Yes",
                        },
                    },
                    numbuttons = 3,
                    colorScheme = null,
                };

                if (SDL.SDL_ShowMessageBox(ref retryMessageBox, out int retryButtonId) < 0)
                    throw new InvalidOperationException($"{nameof(SDL.SDL_ShowMessageBox)} failed: {SDL.SDL_GetError()}");

                switch (retryButtonId) {
                    case 0:
                        Logger.LogLine("User cancelled installation - exiting");
                        return ElevationRequestResult.InstallationCancelled;
                    case 1:
                        Logger.LogLine("User chose to continue installation - running fallback logic");
                        return ElevationRequestResult.Rejected;
                    case 2:
                        Logger.LogLine("Retrying elevated symlink creation");
                        goto retry;
                    default:
                        throw new UnreachableException($"Clicked button ID out of range: {retryButtonId}");
                }

            default:
                throw new UnreachableException($"Clicked button ID out of range: {buttonId}");
        }
    }

    public static bool HandlePostElevationBackup(string[] args) {
        // Handle creating backup symlinks after obtaining elevation
        if (args.Length <= 0 || args[0] != $"{nameof(CreateBackupSymlinksWithElevation)}_PostElevationRequest") return false;
        Globals.PathGame = args[1];
        Globals.PathOrig = args[2];
        BackUp.ShouldCreateBackupSymlinks(out bool content, out bool saves);
        BackUp.CreateBackupSymlinks(content, saves);
        return true;
    }
}
