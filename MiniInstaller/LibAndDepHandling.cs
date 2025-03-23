using Microsoft.NET.HostModel.AppHost;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MiniInstaller;

public static class LibAndDepHandling {
    public static void DeleteSystemLibs() {
        Logger.LogLine("Deleting system libraries");

        foreach (string file in Directory.GetFiles(Globals.PathGame)) {
            if (!MiscUtil.IsSystemLibrary(file))
                continue;
            Logger.LogLine($"Deleting {file}");
            File.Delete(file);
        }
    }

    public static void SetupNativeLibs() {
        string[] libSrcs; // Later entries take priority
        string libDstDir;
        Dictionary<string, string> dllMap = new Dictionary<string, string>();

        switch (Globals.Platform) {
            case Globals.InstallPlatform.Windows: {
                // Setup Windows native libs
                if (Environment.Is64BitOperatingSystem) {
                    libSrcs = new string[] { Path.Combine(Globals.PathEverestLib, "lib64-win-x64"), Path.Combine(Globals.PathGame, "runtimes", "win-x64", "native") };
                    libDstDir = Path.Combine(Globals.PathGame, "lib64-win-x64");
                    dllMap.Add("fmodstudio64.dll", "fmodstudio.dll");
                } else {
                    // We can take some native libraries from the vanilla install
                    libSrcs = new string[] {
                        Path.Combine(Globals.PathOrig, "fmod.dll"), Path.Combine(Globals.PathOrig, "fmodstudio.dll"), Path.Combine(Globals.PathOrig, "steam_api.dll"),
                        Path.Combine(Globals.PathEverestLib, "lib64-win-x86"), Path.Combine(Globals.PathGame, "runtimes", "win-x86", "native")
                    };
                    libDstDir = Path.Combine(Globals.PathGame, "lib64-win-x86");
                }
            } break;
            case Globals.InstallPlatform.Linux: {
                // Setup Linux native libs
                libSrcs = new string[] { Path.Combine(Globals.PathOrig, "lib64"), Path.Combine(Globals.PathEverestLib, "lib64-linux"), Path.Combine(Globals.PathGame, "runtimes", "linux-x64", "native") };
                libDstDir = Path.Combine(Globals.PathGame, "lib64-linux");
                MiscUtil.ParseMonoNativeLibConfig(Path.Combine(Globals.PathOrig, "Celeste.exe.config"), "linux", dllMap, "lib{0}.so");
                MiscUtil.ParseMonoNativeLibConfig(Path.Combine(Globals.PathOrig, "FNA.dll.config"), "linux", dllMap, "lib{0}.so");
            } break;
            case Globals.InstallPlatform.MacOS:{
                // Setup MacOS native libs
                libSrcs = new string[] { Path.Combine(Globals.PathOrig, "osx"), Path.Combine(Globals.PathEverestLib, "lib64-osx"), Path.Combine(Globals.PathGame, "runtimes", "osx", "native") };
                libDstDir = Path.Combine(Globals.PathGame, "lib64-osx");
                MiscUtil.ParseMonoNativeLibConfig(Path.Combine(Globals.PathOrig, "Celeste.exe.config"), "osx", dllMap, "lib{0}.dylib");
                MiscUtil.ParseMonoNativeLibConfig(Path.Combine(Globals.PathOrig, "FNA.dll.config"), "osx", dllMap, "lib{0}.dylib");
            } break;
            default: return;
        }

        // Copy native libraries for the OS
        if (!Directory.Exists(libDstDir))
            Directory.CreateDirectory(libDstDir);

        foreach (string libSrc in libSrcs) {
            if (!Directory.Exists(libSrc))
                continue;

            void CopyNativeLib(string src, string dst) {
                string symlinkPath = null;
                if (dllMap.TryGetValue(Path.GetFileName(dst), out string mappedName)) {
                    // On Linux, additionally create a symlink for the unmapped path
                    // Luckilfy for us only Linux requires such symlinks, as Windows can't create them
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        symlinkPath = dst;

                    dst = Path.Combine(Path.GetDirectoryName(dst), mappedName);
                }

                File.Copy(src, dst, true);

                if (symlinkPath != null && symlinkPath != dst) {
                    File.Delete(symlinkPath);
                    File.CreateSymbolicLink(symlinkPath, Path.GetRelativePath(Path.GetDirectoryName(symlinkPath)!, dst));
                }
            }

            if (File.Exists(libSrc)) {
                string libDst = Path.Combine(libDstDir, Path.GetFileName(libSrc));
                Logger.LogLine($"Copying native library from {libSrc} -> {libDst}");
                CopyNativeLib(libSrc, libDst);
            } else if (Directory.Exists(libSrc)) {
                Logger.LogLine($"Copying native libraries from {libSrc} -> {libDstDir}");
                foreach (string fileSrc in Directory.GetFiles(libSrc))
                    CopyNativeLib(fileSrc, Path.Combine(libDstDir, Path.GetRelativePath(libSrc, fileSrc)));
            }
        }

        // Delete old libraries
        foreach (string libFile in Globals.WindowsNativeLibFileNames)
            File.Delete(Path.Combine(Globals.PathGame, libFile));

        foreach (string libDir in new string[] { "lib", "lib64", "everest-lib64", "runtimes" }) {
            if (Directory.Exists(Path.Combine(Globals.PathGame, libDir)))
                Directory.Delete(Path.Combine(Globals.PathGame, libDir), true);
        }

        if (Globals.PathOSXExecDir != null && Path.Exists(Path.Combine(Globals.PathOSXExecDir, "osx")))
            Directory.Delete(Path.Combine(Globals.PathOSXExecDir, "osx"), true);
        
        // Finally make EverestSplash executable
        if (Globals.Platform is Globals.InstallPlatform.Linux or Globals.InstallPlatform.MacOS) {
            string splashTarget = Globals.Platform switch {
                Globals.InstallPlatform.Linux => "EverestSplash-linux",
                Globals.InstallPlatform.MacOS => "EverestSplash-osx",
                _ => throw new InvalidOperationException(),
            };
            // Permission flags may get overwritten in the packaging process
            Process chmodProc =
                Process.Start(new ProcessStartInfo("chmod", $"u+x \"EverestSplash/{splashTarget}\""));
            chmodProc?.WaitForExit();
            if (chmodProc?.ExitCode != 0) Logger.LogLine("Failed to set EverestSplash executable flag");
        }

    }

    public static void CopyControllerDB() {
        File.Copy(Path.Combine(Globals.PathEverestLib, "gamecontrollerdb.txt"), Path.Combine(Globals.PathGame, "gamecontrollerdb.txt"), true);
        Logger.LogLine("Copied gamecontrollerdb.txt");
    }

    public static void CreateRuntimeConfigFiles(string execAsm, string[] manualDeps = null) {
        manualDeps ??= Array.Empty<string>();

        Logger.LogLine($"Creating .NET runtime configuration files for {execAsm}");

        //Determine current .NET version
        string frameworkName = Assembly.GetExecutingAssembly().GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName;
        if(!frameworkName.StartsWith(".NETCoreApp,Version=v"))
            throw new Exception($"Invalid target framework name! - '{frameworkName}'");

        string netVer = frameworkName.Substring(".NETCoreApp,Version=v".Length);
        if(!Regex.IsMatch(netVer, @"\d+\.\d+"))
            throw new Exception($"Invalid target .NET version! - '{netVer}'");

        //.runtimeconfig.json
        using (FileStream fs = File.OpenWrite(Path.ChangeExtension(execAsm, ".runtimeconfig.json")))
        using (Utf8JsonWriter writer = new Utf8JsonWriter(fs, new JsonWriterOptions() { Indented = true })) {
            writer.WriteStartObject();
            writer.WriteStartObject("runtimeOptions");
            writer.WriteString("tfm", $"net{netVer}");
            writer.WriteStartObject("framework");
            writer.WriteString("name", "Microsoft.NETCore.App");
            writer.WriteString("version", $"{netVer}.0");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        //.deps.json
        Dictionary<string, Dictionary<string, Version>> asms = new Dictionary<string, Dictionary<string, Version>>();

        void DiscoverAssemblies(string asm) {
            if(asms.ContainsKey(asm))
                return;

            Dictionary<string, Version> deps = MiscUtil.GetPEAssemblyReferences(asm);
            asms.Add(asm, deps);

            foreach((string dep, Version _) in deps) {
                string depPath = Path.Combine(Path.GetDirectoryName(asm), $"{dep}.dll");
                if (File.Exists(depPath))
                    DiscoverAssemblies(depPath);
            }
        }
        DiscoverAssemblies(execAsm);
        foreach (string dep in manualDeps)
            DiscoverAssemblies(dep);

        using (FileStream fs = File.OpenWrite(Path.ChangeExtension(execAsm, ".deps.json")))
        using (Utf8JsonWriter writer = new Utf8JsonWriter(fs, new JsonWriterOptions() { Indented = true })) {
            writer.WriteStartObject();

            writer.WriteStartObject("runtimeTarget");
            writer.WriteString("name", frameworkName);
            writer.WriteString("signature", "");
            writer.WriteEndObject();

            writer.WriteStartObject("compilationOptions");
            writer.WriteEndObject();

            writer.WriteStartObject("targets");
            writer.WriteStartObject(frameworkName);
            foreach ((string asmPath, Dictionary<string, Version> asmDeps) in asms) {
                writer.WriteStartObject($"{Path.GetFileNameWithoutExtension(asmPath)}/{MiscUtil.GetPEAssemblyVersion(asmPath)}");

                writer.WriteStartObject("runtime");
                writer.WriteStartObject(Path.GetFileName(asmPath));
                writer.WriteEndObject();
                writer.WriteEndObject();

                if (asmDeps.Count > 0) {
                    writer.WriteStartObject("dependencies");
                    foreach (var dep in asmDeps)
                        writer.WriteString(dep.Key, dep.Value.ToString());
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteStartObject("libraries");
            foreach ((string asmPath, Dictionary<string, Version> asmDeps) in asms) {
                writer.WriteStartObject($"{Path.GetFileNameWithoutExtension(asmPath)}/{MiscUtil.GetPEAssemblyVersion(asmPath)}");
                writer.WriteString("type", (asmPath == execAsm) ? "project" : "reference");
                writer.WriteBoolean("servicable", false);
                writer.WriteString("sha512", string.Empty);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();

            writer.WriteEndObject();
        }
    }

    public static void SetupAppHosts(string appExe, string appDll, string resDll = null) {
        // We only support setting copying the host resources on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            resDll = null;

        // Delete MonoKickstart files
        File.Delete(Path.ChangeExtension(appExe, ".bin.x86"));
        File.Delete(Path.ChangeExtension(appExe, ".bin.x86_64"));
        File.Delete($"{appExe}.config");
        File.Delete(Path.Combine(Path.GetDirectoryName(appExe), "monoconfig"));
        File.Delete(Path.Combine(Path.GetDirectoryName(appExe), "monomachineconfig"));
        File.Delete(Path.Combine(Path.GetDirectoryName(appExe), "FNA.dll.config"));

        string hostsDir = Path.Combine(Globals.PathGame, "piton-apphosts");

        switch (Globals.Platform) {
            case Globals.InstallPlatform.Windows: {
                // Bind Windows apphost
                Logger.LogLine($"Binding Windows {(Environment.Is64BitOperatingSystem ? "64" : "32")} bit apphost {appExe}");
                HostWriter.CreateAppHost(
                    Path.Combine(hostsDir, $"win.{(Environment.Is64BitOperatingSystem ? "x64" : "x86")}.exe"),
                    appExe, Path.GetRelativePath(Path.GetDirectoryName(appExe), appDll),
                    assemblyToCopyResorcesFrom: resDll,
                    windowsGraphicalUserInterface: true
                );
            } break;
            case Globals.InstallPlatform.Linux:{
                // Bind Linux apphost
                Logger.LogLine($"Binding Linux apphost {Path.ChangeExtension(appExe, null)}");
                HostWriter.CreateAppHost(Path.Combine(hostsDir, "linux"), Path.ChangeExtension(appExe, null), Path.GetRelativePath(Path.GetDirectoryName(appExe), appDll));
            } break;
            case Globals.InstallPlatform.MacOS: {
                // Bind OS X apphost
                Logger.LogLine($"Binding OS X apphost {Path.ChangeExtension(appExe, null)}");
                HostWriter.CreateAppHost(Path.Combine(hostsDir, "osx"), Path.ChangeExtension(appExe, null), Path.GetRelativePath(Path.GetDirectoryName(appExe), appDll));

                File.Delete(Path.Combine(Globals.PathOSXExecDir, Path.GetFileNameWithoutExtension(appExe)));
                File.CreateSymbolicLink(Path.Combine(Globals.PathOSXExecDir, Path.GetFileNameWithoutExtension(appExe)),
                                        Path.GetRelativePath(Globals.PathOSXExecDir, Path.ChangeExtension(appExe, null)));
            } break;
        }
    }
}