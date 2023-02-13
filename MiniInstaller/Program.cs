using Celeste.Mod.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace MiniInstaller {
    public class Program {

        public static string PathUpdate;
        public static string PathGame;
        public static string PathCelesteExe;
        public static string PathEverestExe, PathEverestDLL;
        public static string PathOrig;
        public static string PathDylibs;
        public static string PathLog;

        public static Assembly AsmMonoMod;
        public static Assembly AsmHookGen;
        public static Assembly AsmNETCoreifier;

        // This can be set from the in-game installer via reflection.
        public static Action<string> LineLogger;
        public static void LogLine(string line) {
            LineLogger?.Invoke(line);
            Console.WriteLine(line);
        }

        public static void LogErr(string line) {
            LineLogger?.Invoke(line);
            Console.Error.WriteLine(line);
        }

        public static int Main(string[] args) {
            if (Type.GetType("Mono.Runtime") != null) {
                Console.WriteLine("MiniInstaller is unable to run under mono!");
                return 1;
            }

            if (Path.GetFileName(Environment.ProcessPath) == "dotnet") {
                // The .dll has been executed directly using 'dotnet MiniInstaller.dll'
                // Bind MiniInstaller apphosts
                Console.WriteLine("Binding MiniInstaller apphosts... ");

                string dllPath = Assembly.GetExecutingAssembly().Location;
                PathGame = Path.GetDirectoryName(dllPath); // Used to reference the apphost binaries

                BindAppHost("win.exe", Path.ChangeExtension(dllPath, null) + "-win.exe", dllPath);
                BindAppHost("linux", Path.ChangeExtension(dllPath, null) + "-linux", dllPath);
                BindAppHost("osx", Path.ChangeExtension(dllPath, null) + "-osx", dllPath);
                return 0;
            }

            Console.WriteLine("Everest MiniInstaller");

            if (!SetupPaths()) {
                // setting up paths failed (Celeste.exe was not found).
                return 1;
            }

            // .NET hates it when strong-named dependencies get updated.
            AppDomain.CurrentDomain.AssemblyResolve += (asmSender, asmArgs) => {
                AssemblyName asmName = new AssemblyName(asmArgs.Name);
                if (!asmName.Name.StartsWith("Mono.Cecil"))
                    return null;

                Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(other => other.GetName().Name == asmName.Name);
                if (asm != null)
                    return asm;

                if (PathUpdate != null)
                    return Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(PathUpdate), asmName.Name + ".dll"));

                return null;
            };

            if (File.Exists(PathLog))
                File.Delete(PathLog);
            using (Stream fileStream = File.OpenWrite(PathLog))
            using (StreamWriter fileWriter = new StreamWriter(fileStream, Console.OutputEncoding))
            using (LogWriter logWriter = new LogWriter {
                STDOUT = Console.Out,
                File = fileWriter
            }) {
                Console.SetOut(logWriter);

                try {
                    WaitForGameExit();

                    Backup();

                    MoveFilesFromUpdate();

                    DeleteSystemLibs();
                    SetupNativeLibs();

                    if (AsmMonoMod == null || AsmNETCoreifier == null) {
                        LoadModders();
                    }
                    RunMonoMod(Path.Combine(PathOrig, "Celeste.exe"), PathEverestExe, new string[] { Path.ChangeExtension(PathCelesteExe, ".Mod.mm.dll") });
                    ConvertToNETCore(PathEverestExe);
                    RunHookGen(PathEverestExe, PathCelesteExe);

                    MoveExecutable(PathEverestExe, PathEverestDLL);
                    CreateRuntimeConfigFiles(PathEverestDLL, new string[] {
                        Path.ChangeExtension(PathCelesteExe, ".Mod.mm.dll"),
                        Path.Combine(Path.GetDirectoryName(PathCelesteExe), "MMHOOK_" + Path.ChangeExtension(Path.GetFileName(PathCelesteExe), ".dll"))
                    });
                    SetupAppHosts(PathEverestExe, PathEverestDLL);

                    CombineXMLDoc(Path.ChangeExtension(PathCelesteExe, ".Mod.mm.xml"), Path.ChangeExtension(PathCelesteExe, ".xml"));


                    // If we're updating, start the game. Otherwise, close the window.
                    if (PathUpdate != null) {
                        StartGame();
                    }

                } catch (Exception e) {
                    string msg = e.ToString();
                    LogLine("");
                    LogErr(msg);
                    LogErr("");
                    LogErr("Installing Everest failed.");
                    if (msg.Contains("MonoMod failed relinking Microsoft.Xna.Framework") ||
                        msg.Contains("MonoModRules failed resolving Microsoft.Xna.Framework.Game")) {
                        LogErr("Please run the game at least once to install missing dependencies.");
                    } else {
                        if (msg.Contains("--->")) {
                            LogErr("Please review the error after the '--->' to see if you can fix it on your end.");
                        }
                        LogErr("");
                        LogErr("If you need help, please create a new issue on GitHub @ https://github.com/EverestAPI/Everest");
                        LogErr("or join the #modding_help channel on Discord (invite in the repo).");
                        LogErr("Make sure to upload your log file.");
                    }
                    return 1;

                } finally {
                    // Let's not pollute <insert installer name here>.
                    Environment.SetEnvironmentVariable("MONOMOD_DEPDIRS", "");
                    Environment.SetEnvironmentVariable("MONOMOD_MODS", "");
                    Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "");
                }

                Console.SetOut(logWriter.STDOUT);
                logWriter.STDOUT = null;
            }

            return 0;
        }

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
                LogErr("Celeste.exe not found!");
                LogErr("Did you extract the .zip into the same place as Celeste?");
                return false;
            }

            // Here lies a reminder that patching into Everest.exe only caused confusion and issues.
            // RIP Everest.exe 2019 - 2020
            PathEverestExe = PathCelesteExe;
            PathEverestDLL = Path.ChangeExtension(PathEverestExe, ".dll");

            PathOrig = Path.Combine(PathGame, "orig");
            PathLog = Path.Combine(PathGame, "miniinstaller-log.txt");

            if (!Directory.Exists(Path.Combine(PathGame, "Mods"))) {
                LogLine("Creating Mods directory");
                Directory.CreateDirectory(Path.Combine(PathGame, "Mods"));
            }

            // Can't check for platform as some morons^Wuninformed people could be running MiniInstaller via wine.
            if (PathGame.Replace(Path.DirectorySeparatorChar, '/').Trim('/').EndsWith(".app/Contents/Resources")) {
                PathDylibs = Path.Combine(Path.GetDirectoryName(PathGame), "MacOS", "osx");
                if (!Directory.Exists(PathDylibs))
                    PathDylibs = null;
            }

            return true;
        }

        public static void WaitForGameExit() {
            if (
                (File.Exists(PathEverestExe) && !CanReadWrite(PathEverestExe)) ||
                (File.Exists(PathEverestDLL) && !CanReadWrite(PathEverestDLL))
             ) {
                LogErr("Celeste not read-writeable - waiting");
                while (!CanReadWrite(PathCelesteExe))
                    Thread.Sleep(5000);
            }
        }

        public static void Backup() {
            // Backup / restore the original game files we're going to modify.
            // TODO: Maybe invalidate the orig dir when the backed up version < installed version?
            if (!Directory.Exists(PathOrig)) {
                LogLine("Creating backup orig directory");
                Directory.CreateDirectory(PathOrig);
            }

            //Backup the game executable
            Backup(PathCelesteExe);
            Backup(PathCelesteExe + ".pdb");
            Backup(Path.ChangeExtension(PathCelesteExe, "mdb"));

            //Backup game dependencies
            BackupPEDeps(Path.Combine(PathOrig, Path.GetRelativePath(PathGame, PathCelesteExe)), PathGame);

            //Backup all system libraries explicitly, as we'll delete those
            foreach (string file in Directory.GetFiles(PathGame)) {
                if(IsSystemLibrary(file))
                    Backup(file);
            }

            //Backup MonoKickstart executable / config (for Linux + MacOS)
            Backup(Path.Combine(PathGame, "Celeste"));
            if (PathDylibs != null)
                Backup(Path.Combine(Path.GetDirectoryName(PathDylibs), "Celeste"));
            Backup(Path.Combine(PathGame, "Celeste.bin.x86"));
            Backup(Path.Combine(PathGame, "Celeste.bin.x86_64"));
            Backup(Path.Combine(PathGame, "monoconfig"));
            Backup(Path.Combine(PathGame, "monomachineconfig"));
            Backup(Path.Combine(PathGame, "FNA.dll.config"));

            //Backup native libraries
            Backup(Path.Combine(PathGame, "fmod.dll"));
            Backup(Path.Combine(PathGame, "fmodstudio.dll"));
            Backup(Path.Combine(PathGame, "steam_api.dll"));
            Backup(Path.Combine(PathGame, "FNA3D.dll"));
            Backup(Path.Combine(PathGame, "SDL2.dll"));
            Backup(Path.Combine(PathGame, "lib"));
            Backup(Path.Combine(PathGame, "lib64"));

            //Backup misc files
            Backup(PathCelesteExe + ".config");
            Backup(Path.Combine(PathGame, "gamecontrollerdb.txt"));

            //Create a symlink for the contents folder
            if (!Directory.Exists(Path.Combine(PathOrig, "Content"))) {
                try {
                    Directory.CreateSymbolicLink(Path.Combine(PathOrig, "Content"), Path.Combine(PathGame, "Content"));
                } catch (IOException) {
                    static void CopyDirectory(string src, string dst) {
                        foreach (string file in Directory.GetFiles(src))
                            File.Copy(file, Path.Combine(dst, Path.GetRelativePath(src, file)));

                        foreach (string dir in Directory.GetDirectories(src))
                            CopyDirectory(dir, Path.Combine(dst, Path.GetRelativePath(src, dir)));
                    }
                    CopyDirectory(Path.Combine(PathOrig, "Content"), Path.Combine(PathGame, "Content"));
                }
            }
        }

        public static void BackupPEDeps(string path, string depFolder, HashSet<string> backedUpDeps = null) {
            backedUpDeps ??= new HashSet<string>() { path };

            foreach (string dep in GetPEAssemblyReferences(path).Keys) {
                string asmRefPath = Path.Combine(depFolder, $"{dep}.dll");
                if (!File.Exists(asmRefPath) || backedUpDeps.Contains(asmRefPath))
                    continue;

                backedUpDeps.Add(asmRefPath);
                Backup(asmRefPath);
                BackupPEDeps(asmRefPath, depFolder, backedUpDeps);
            }
        }

        public static void Backup(string from, string backupDst = null) {
            string to = Path.Combine(backupDst ?? PathOrig, Path.GetFileName(from));
            if(Directory.Exists(from)) {
                if (!Directory.Exists(to))
                    Directory.CreateDirectory(to);

                foreach (string entry in Directory.GetFileSystemEntries(from))
                    Backup(entry, to);
            } else if(File.Exists(from)) {
                if (File.Exists(from) && !File.Exists(to)) {
                    LogLine($"Backing up {from} => {to}");
                    File.Copy(from, to);
                }
            }
        }

        public static void MoveFilesFromUpdate(string srcPath = null, string dstPath = null) {
            if (srcPath == null) {
                if (PathUpdate == null)
                    return;

                LogLine("Moving files from update directory");
                srcPath ??= PathUpdate;
                dstPath ??= PathGame;
            }

            if (!Directory.Exists(dstPath))
                Directory.CreateDirectory(dstPath);

            foreach (string entrySrc in Directory.GetFileSystemEntries(srcPath)) {
                string entryDst = Path.Combine(dstPath, Path.GetRelativePath(srcPath, entrySrc));

                if (File.Exists(entrySrc)) {
                    LogLine($"Copying {entrySrc} +> {entryDst}");
                    File.Copy(entrySrc, entryDst, true);
                } else
                    MoveFilesFromUpdate(entrySrc, entryDst);
            }
        }

        public static void DeleteSystemLibs() {
            LogLine("Deleting system libraries");

            foreach (string file in Directory.GetFiles(PathGame)) {
                if (!IsSystemLibrary(file))
                    continue;
                LogLine($"Deleting {file}");
                File.Delete(file);
            }
        }

        public static void SetupNativeLibs() {
            string[] libSrcDirs;
            string libDstDir;
            bool strip64NameSuffix = false, stripSoVersionSuffix = false;

            if (PathDylibs != null) {
                // Setup MacOS native libs
                libSrcDirs = new string[] { Path.Combine(PathGame, "lib64-osx"), Path.Combine(PathGame, "runtimes", "osx", "native") };
                libDstDir = PathDylibs;
            } if (File.Exists(Path.ChangeExtension(PathCelesteExe, null))) {
                // Setup Linux native libs
                libSrcDirs = new string[] { Path.Combine(PathGame, "lib64"), Path.Combine(PathGame, "lib64-linux"), Path.Combine(PathGame, "runtimes", "linux-x64", "native") };
                libDstDir = PathGame;
                stripSoVersionSuffix = true;
            } else {
                // Setup Windows native libs
                libSrcDirs = new string[] { Path.Combine("lib64-win"), Path.Combine(PathGame, "runtimes", "win-x64", "native") };
                libDstDir = PathGame;
                strip64NameSuffix = true;
            }

            // Copy native libraries for the OS
            foreach (string libSrcDir in libSrcDirs) {
                if (!Directory.Exists(libSrcDir))
                    continue;

                LogLine($"Copying native libraries from {libSrcDir} -> {libDstDir}");

                foreach (string fileSrc in Directory.GetFiles(libSrcDir)) {
                    string fileDst = Path.Combine(libDstDir, Path.GetRelativePath(libSrcDir, fileSrc));

                    if (strip64NameSuffix && Path.GetFileNameWithoutExtension(fileDst).EndsWith("64")) {
                        // Remove XYZ64.dll suffix to make the target name XYZ.dll
                        string fname = Path.GetFileNameWithoutExtension(fileDst);
                        fname = fname.Substring(0, fname.Length - 2);
                        if (Path.HasExtension(fileDst))
                            fname += "." + Path.GetExtension(fileDst);

                        fileDst = Path.Combine(Path.GetDirectoryName(fileDst), fname);
                    }

                    File.Copy(fileSrc, fileDst, true);

                    if (stripSoVersionSuffix) {
                        // Create symlinks for .so files without their suffix
                        int soIdx = fileDst.LastIndexOf(".so");
                        if (0 <= soIdx && soIdx < fileDst.Length - 3) {
                            File.Delete(fileDst.Substring(0, soIdx + 3));
                            File.CreateSymbolicLink(fileDst.Substring(0, soIdx + 3), fileDst);
                        }
                    }
                }
            }

            // Delete library folders
            foreach (string libDirName in new string[] { "lib", "lib64", "lib64-win", "lib64-linux", "lib64-osx", "runtimes" }) {
                if (Directory.Exists(Path.Combine(PathGame, libDirName)))
                    Directory.Delete(Path.Combine(PathGame, libDirName), true);
            }
        }

        public static void LoadModders() {
            // We can't add MonoMod as a reference to MiniInstaller, as we don't want to accidentally lock the file.
            // Instead, load it dynamically and invoke the entry point.
            // We also need to lazily load any dependencies.
            LogLine("Loading Mono.Cecil");
            LazyLoadAssembly(Path.Combine(PathGame, "Mono.Cecil.dll"));
            LogLine("Loading Mono.Cecil.Mdb");
            LazyLoadAssembly(Path.Combine(PathGame, "Mono.Cecil.Mdb.dll"));
            LogLine("Loading Mono.Cecil.Pdb");
            LazyLoadAssembly(Path.Combine(PathGame, "Mono.Cecil.Pdb.dll"));
            LogLine("Loading MonoMod.Utils.dll");
            LazyLoadAssembly(Path.Combine(PathGame, "MonoMod.Utils.dll"));
            LogLine("Loading MonoMod");
            AsmMonoMod = LazyLoadAssembly(Path.Combine(PathGame, "MonoMod.Patcher.dll"));
            LogLine("Loading MonoMod.RuntimeDetour.dll");
            LazyLoadAssembly(Path.Combine(PathGame, "MonoMod.RuntimeDetour.dll"));
            LogLine("Loading MonoMod.RuntimeDetour.HookGen");
            AsmHookGen = LazyLoadAssembly(Path.Combine(PathGame, "MonoMod.RuntimeDetour.HookGen.dll"));
            LogLine("Loading NETCoreifier");
            AsmNETCoreifier = LazyLoadAssembly(Path.Combine(PathGame, "NETCoreifier.dll"));
        }

        public static void RunMonoMod(string asmFrom, string asmTo = null, string[] dllPaths = null) {
            asmTo ??= asmFrom;
            dllPaths ??= new string[] { PathGame };

            LogLine($"Running MonoMod for {asmFrom}");
            // We're lazy.
            Environment.SetEnvironmentVariable("MONOMOD_DEPDIRS", PathGame);
            Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "0");
            int returnCode = (int) AsmMonoMod.EntryPoint.Invoke(null, new object[] { Enumerable.Repeat(asmFrom, 1).Concat(dllPaths).Append(asmTo + ".tmp").ToArray() });

            if (returnCode != 0 && File.Exists(asmTo + ".tmp"))
                File.Delete(asmTo + ".tmp");

            if (!File.Exists(asmTo + ".tmp"))
                throw new Exception("MonoMod failed creating a patched assembly!");

            MoveExecutable(asmTo + ".tmp", asmTo);
        }

        public static void RunHookGen(string asm, string target) {
            LogLine($"Running MonoMod.RuntimeDetour.HookGen for {asm}");
            // We're lazy.
            Environment.SetEnvironmentVariable("MONOMOD_DEPDIRS", PathGame);
            Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "0");
            AsmHookGen.EntryPoint.Invoke(null, new object[] { new string[] { "--private", asm, Path.Combine(Path.GetDirectoryName(target), "MMHOOK_" + Path.ChangeExtension(Path.GetFileName(target), "dll")) } });
        }

        public static void ConvertToNETCore(string asm, HashSet<string> convertedAsms = null) {
            convertedAsms ??= new HashSet<string>();

            if (!convertedAsms.Add(asm))
                return;

            // Convert dependencies first
            foreach (string dep in GetPEAssemblyReferences(asm).Keys) {
                string depPath = Path.Combine(Path.GetDirectoryName(asm), $"{dep}.dll");
                if (File.Exists(depPath) && !IsSystemLibrary(depPath))
                    ConvertToNETCore(depPath, convertedAsms);
            }

            LogLine($"Converting {asm} to .NET Core");

            AsmNETCoreifier.GetType("NETCoreifier.Coreifier")
                .GetMethod("ConvertToNetCore", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(string) }, null)
                .Invoke(null, new object[] { asm, asm + ".tmp" });

            File.Delete(asm);
            File.Move(asm + ".tmp", asm);
        }

        public static void MoveExecutable(string srcPath, string dstPath) {
            File.Delete(dstPath);
            File.Move(srcPath, dstPath);

            if (Path.GetFullPath(Path.ChangeExtension(srcPath, null)) != Path.GetFullPath(Path.ChangeExtension(dstPath, null))) {
                if (File.Exists(Path.ChangeExtension(srcPath, ".pdb"))) {
                    File.Delete(Path.ChangeExtension(dstPath, ".pdb"));
                    File.Move(Path.ChangeExtension(srcPath, ".pdb"), Path.ChangeExtension(dstPath, ".pdb"));
                }

                if (File.Exists(Path.ChangeExtension(srcPath, ".mdb"))) {
                    File.Delete(Path.ChangeExtension(dstPath, ".mdb"));
                    File.Move(Path.ChangeExtension(srcPath, ".mdb"), Path.ChangeExtension(dstPath, ".mdb"));
                }
            }
        }

        public static void CreateRuntimeConfigFiles(string execAsm, string[] manualDeps = null) {
            manualDeps ??= Array.Empty<string>();

            LogLine($"Creating .NET runtime configuration files for {execAsm}");

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

                Dictionary<string, Version> deps = GetPEAssemblyReferences(asm);
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
                    writer.WriteStartObject($"{Path.GetFileNameWithoutExtension(asmPath)}/{GetPEAssemblyVersion(asmPath)}");

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
                    writer.WriteStartObject($"{Path.GetFileNameWithoutExtension(asmPath)}/{GetPEAssemblyVersion(asmPath)}");
                    writer.WriteString("type", (asmPath == execAsm) ? "project" : "reference");
                    writer.WriteBoolean("servicable", false);
                    writer.WriteString("sha512", string.Empty);
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();

                writer.WriteEndObject();
            }
        }

        public static void SetupAppHosts(string appExe, string appDll) {
            // Delete MonoKickstart files
            File.Delete(Path.ChangeExtension(appExe, ".bin.x86"));
            File.Delete(Path.ChangeExtension(appExe, ".bin.x86_64"));
            File.Delete($"{appExe}.config");
            File.Delete(Path.Combine(Path.GetDirectoryName(appExe), "monoconfig"));
            File.Delete(Path.Combine(Path.GetDirectoryName(appExe), "monomachineconfig"));
            File.Delete(Path.Combine(Path.GetDirectoryName(appExe), "FNA.dll.config"));

            // Bind Windows apphost
            LogLine($"Binding Windows apphost {appExe}");
            BindAppHost("win.exe", appExe, appDll);

            // Bind Linux apphost (if it exists)
            if (File.Exists(Path.ChangeExtension(appExe, null))) {
                LogLine($"Binding Linux apphost {Path.ChangeExtension(appExe, null)}");
                BindAppHost("linux", Path.ChangeExtension(appExe, null), appDll);
            }

            // Bind OS X apphost (if it exists)
            if (PathDylibs != null) {
                LogLine($"Binding OS X apphost {Path.Combine(Path.GetDirectoryName(PathDylibs), Path.GetFileNameWithoutExtension(appExe))}");
                BindAppHost("osx", Path.Combine(Path.GetDirectoryName(PathDylibs), Path.GetFileNameWithoutExtension(appExe)), appDll);
            }
        }

        public static void StartGame() {
            LogLine("Restarting Celeste");

            // Let's not pollute the game with our MonoMod env vars.
            Environment.SetEnvironmentVariable("MONOMOD_DEPDIRS", "");
            Environment.SetEnvironmentVariable("MONOMOD_MODS", "");
            Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "");

            Process game = new Process();
            // If the game was installed via Steam, it should restart in a Steam context on its own.
            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX) {
                // The Linux and macOS version apphosts don't end in ".exe"
                // Additionaly, the macOS apphost is outside the game files folder
                if (PathDylibs != null)
                    game.StartInfo.FileName = Path.Combine(Path.GetDirectoryName(PathDylibs), Path.GetFileNameWithoutExtension(PathEverestExe));
                else
                    game.StartInfo.FileName = Path.ChangeExtension(PathEverestExe, null);
            } else {
                game.StartInfo.FileName = PathEverestExe;
            }
            game.StartInfo.WorkingDirectory = PathGame;
            game.Start();
        }


        // AFAIK there's no "clean" way to check for any file locks in C#.
        static bool CanReadWrite(string path) {
            try {
                new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete).Dispose();
                return true;
            } catch {
                return false;
            }
        }

        static bool IsSystemLibrary(string file) {
            if (Path.GetExtension(file) != ".dll")
                return false;

            if (Path.GetFileName(file).StartsWith("System."))
                return true;

            return new string[] {
                "mscorlib.dll",
                "Mono.Posix.dll",
                "Mono.Security.dll"
            }.Any(name => Path.GetFileName(file).Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        static Assembly LazyLoadAssembly(string path) {
            LogLine($"Lazily loading {path}");
            ResolveEventHandler tmpResolver = (s, e) => {
                string asmPath = Path.Combine(Path.GetDirectoryName(path), new AssemblyName(e.Name).Name + ".dll");
                if (!File.Exists(asmPath))
                    return null;
                return Assembly.LoadFrom(asmPath);
            };
            AppDomain.CurrentDomain.AssemblyResolve += tmpResolver;
            Assembly asm = Assembly.Load(Path.GetFileNameWithoutExtension(path));
            AppDomain.CurrentDomain.AssemblyResolve -= tmpResolver;
            AppDomain.CurrentDomain.TypeResolve += (s, e) => {
                return asm.GetType(e.Name) != null ? asm : null;
            };
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
                return e.Name == asm.FullName || e.Name == asm.GetName().Name ? asm : null;
            };
            return asm;
        }

        static Version GetPEAssemblyVersion(string path) {
            using (FileStream fs = File.OpenRead(path))
            using (PEReader pe = new PEReader(fs))
                return pe.GetMetadataReader().GetAssemblyDefinition().Version;
        }

        static Dictionary<string, Version> GetPEAssemblyReferences(string path) {
            using (FileStream fs = File.OpenRead(path))
            using (PEReader pe = new PEReader(fs)) {
                MetadataReader meta = pe.GetMetadataReader();

                Dictionary<string, Version> deps = new Dictionary<string, Version>();
                foreach (AssemblyReference asmRef in meta.AssemblyReferences.Select(meta.GetAssemblyReference))
                    deps.TryAdd(meta.GetString(asmRef.Name), asmRef.Version);

                return deps;
            }
        }

        static void BindAppHost(string hostName, string outPath, string appPath) {
            appPath = Path.GetRelativePath(Path.GetDirectoryName(outPath), appPath);

            byte[] appHost = File.ReadAllBytes(Path.Combine(PathGame, "apphosts", hostName));

            byte[] placeholder = Encoding.UTF8.GetBytes("c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2"); //SHA256 of "foobar"
            int pathIdx = appHost.AsSpan().IndexOf(placeholder);
            if (pathIdx < 0)
                throw new Exception("Couldn't bind apphost!");

            int appPathLen = Encoding.UTF8.GetBytes(appPath, 0, appPath.Length, appHost, pathIdx);
            if (appPathLen > placeholder.Length)
                throw new ArgumentException("Apphost application path is too long!");

            Array.Fill<byte>(appHost, 0, pathIdx + appPathLen, placeholder.Length - appPathLen);

            File.WriteAllBytes(outPath, appHost);

            // Set the executable flag
            // TODO Check if this actually works on MacOS
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("/bin/sh", new string[] { "-c", $"chmod ug+x \"{Path.GetFullPath(outPath)}\"" }).WaitForExit();
        }

        static void CombineXMLDoc(string xmlFrom, string xmlTo) {
            LogLine("Combining Documentation");
            XmlDocument from = new XmlDocument();
            XmlDocument to = new XmlDocument();

            // Not worth crashing over.
            try {
                from.Load(xmlFrom);
                to.Load(xmlTo);
            } catch (FileNotFoundException e) {
                LogLine(e.Message);
                LogErr("Documentation combining aborted.");
                return;
            }

            XmlNodeList members = from.DocumentElement.LastChild.ChildNodes;

            // Reverse for loop so that we can remove nodes without breaking everything
            for (int i = members.Count - 1; i >= 0; i--) {
                XmlNode node = members[i];
                XmlAttribute name = node.Attributes["name"];
                string noPatch = name.Value.Replace("patch_", "");
                if (!noPatch.Equals(name.Value)) {
                    // Remove internal inheritdoc members that would otherwise override "vanilla" celeste members.
                    if (node.SelectNodes($"inheritdoc[@cref='{noPatch}']").Count == 1) {
                        node.ParentNode.RemoveChild(node);
                        continue;
                    }
                    name.Value = noPatch;
                }

                // Fix up any references to patch_ class members.
                foreach (XmlAttribute cref in node.SelectNodes(".//@cref"))
                    cref.Value = cref.Value.Replace("patch_", "");

                // I couldn't find a way to just do this for all orig_ methods, so an <origdoc/> tag needs to be manually added to them.
                // And of course there also doesn't seem to be support for adding custom tags to the xmldoc prompts -_-
                if (node.ChildNodes.Count == 1 && node.FirstChild.LocalName.Equals("origdoc")) {
                    XmlNode origDoc = from.CreateElement("summary");
                    CreateOrigDoc(node.FirstChild, ref origDoc);
                    node.RemoveChild(node.FirstChild);
                    node.AppendChild(origDoc);
                }
            }

            // Remove any pre-existing Everest docs
            members = to.DocumentElement.ChildNodes;
            for (int i = members.Count - 1; i >= 0; i--) {
                XmlNode node = members[i];
                if (node.Attributes?["name"] != null && node.Attributes["name"].Value == "Everest") {
                    to.DocumentElement.RemoveChild(node);
                }
            }

            // Add an Everest tag onto the docs to be added
            XmlAttribute attrib = from.CreateAttribute("name");
            attrib.Value = "Everest";
            from.DocumentElement.LastChild.Attributes.Append(attrib);

            to.DocumentElement.AppendChild(to.ImportNode(from.DocumentElement.LastChild, true));
            to.Save(xmlTo);
        }

        static void CreateOrigDoc(XmlNode node, ref XmlNode origDoc) {
            string cref = node.Attributes["cref"]?.Value;
            if (cref == null) {
                cref = node.ParentNode.Attributes["name"].Value.Replace("orig_", "");
            }

            origDoc.InnerXml = "Vanilla Method. Use <see cref=\"" + cref + "\"/> instead.";
        }

    }
}
