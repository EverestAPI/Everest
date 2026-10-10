using MiniInstaller.SDL2;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MiniInstaller;

public static class Interop {
    public static void SetupImportResolver() {
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), LibraryImportResolver);
    }

    private static nint LibraryImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) {
        if (libraryName != SDL.nativeLibName)
            return nint.Zero;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return RuntimeInformation.OSArchitecture switch {
                Architecture.X64 => NativeLibrary.Load(Path.Combine(Globals.PathEverestLib, Globals.LibWinX64, "SDL2.dll")),
                Architecture.X86 => NativeLibrary.Load(Path.Combine(Globals.PathEverestLib, Globals.LibWinX86, "SDL2.dll")),
                _ => nint.Zero,
            };
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return NativeLibrary.Load(Path.Combine(Globals.PathEverestLib, Globals.LibLinux, "libSDL2-2.0.so.0"));
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return NativeLibrary.Load(Path.Combine(Globals.PathEverestLib, Globals.LibMacOS, "libSDL2-2.0.0.dylib"));

        return nint.Zero;
    }
}
