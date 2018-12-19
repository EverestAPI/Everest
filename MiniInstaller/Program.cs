using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Celeste.Mod;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using Celeste.Mod.Helpers;

namespace MiniInstaller {
    public class Program {

        public static string PathUpdate;
        public static string PathGame;
        public static string PathCelesteExe;
        public static string PathOrig;
        public static string PathLog;

        public static Assembly AsmMonoMod;
        public static Assembly AsmHookGen;

        // This can be set from the in-game installer via reflection.
        public static Action<string> LineLogger;
        public static void LogLine(string line) {
            LineLogger?.Invoke(line);
            Console.WriteLine(line);
        }

        public static int Main(string[] args) {
            Console.WriteLine("Everest MiniInstaller");

            SetupPaths();

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

                    if (AsmMonoMod == null) {
                        LoadMonoMod();
                    }
                    RunMonoMod(Path.Combine(PathOrig, "Celeste.exe"), PathCelesteExe);
                    RunHookGen(PathCelesteExe);

                    // If we're updating, start the game. Otherwise, close the window. 
                    if (PathUpdate != null) {
                        StartGame();
                    }

                } catch (Exception e) {
                    LogLine("");
                    LogLine(e.ToString());
                    LogLine("");
                    LogLine("Installing Everest failed.");
                    LogLine("Please create a new issue on GitHub @ https://github.com/EverestAPI/Everest");
                    LogLine("or join the #game_modding channel on Discord (invite in the repo).");
                    LogLine("Make sure to upload your miniinstaller-log.txt");
                    return 1;
                }

                Console.SetOut(logWriter.STDOUT);
                logWriter.STDOUT = null;
            }

            return 0;
        }

        public static void SetupPaths() {
            PathGame = Directory.GetCurrentDirectory();

            if (Path.GetFileName(PathGame) == "everest-update" &&
                File.Exists(Path.Combine(Path.GetDirectoryName(PathGame), "Celeste.exe"))) {
                // We're updating Everest via the in-game installler.
                PathUpdate = PathGame;
                PathGame = Path.GetDirectoryName(PathUpdate);
            }

            PathCelesteExe = Path.Combine(PathGame, "Celeste.exe");
            if (!File.Exists(PathCelesteExe)) {
                LogLine("Celeste.exe not found!");
                LogLine("Did you extract the .zip into the same place as Celeste?");
                return;
            }

            PathOrig = Path.Combine(PathGame, "orig");
            PathLog = Path.Combine(PathGame, "miniinstaller-log.txt");

            if (!Directory.Exists(Path.Combine(PathGame, "Mods"))) {
                LogLine("Creating Mods directory");
                Directory.CreateDirectory(Path.Combine(PathGame, "Mods"));
            }
        }

        public static void WaitForGameExit() {
            if (!CanReadWrite(PathCelesteExe)) {
                LogLine("Celeste not read-writeable - waiting");
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
                File.Copy(PathCelesteExe, Path.Combine(PathOrig, "Celeste.exe"));
                if (File.Exists(PathCelesteExe + ".pdb"))
                    File.Copy(PathCelesteExe + ".pdb", Path.Combine(PathOrig, "Celeste.exe.pdb"));
                if (File.Exists(Path.ChangeExtension(PathCelesteExe, "mdb")))
                    File.Copy(Path.ChangeExtension(PathCelesteExe, "mdb"), Path.Combine(PathOrig, "Celeste.mdb"));
            }
        }

        public static void MoveFilesFromUpdate() {
            if (PathUpdate != null) {
                LogLine("Moving files from update directory");
                foreach (string fileUpdate in Directory.GetFiles(PathUpdate)) {
                    string fileRelative = fileUpdate.Substring(PathUpdate.Length + 1);
                    string fileGame = Path.Combine(PathGame, fileRelative);
                    LogLine($"Copying {fileUpdate} +> {fileGame}");
                    File.Copy(fileUpdate, fileGame, true);
                }
            }
        }

        public static void LoadMonoMod() {
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
            AsmMonoMod = LazyLoadAssembly(Path.Combine(PathGame, "MonoMod.exe"));
            LogLine("Loading MonoMod.RuntimeDetour.dll");
            LazyLoadAssembly(Path.Combine(PathGame, "MonoMod.RuntimeDetour.dll"));
            LogLine("Loading MonoMod.RuntimeDetour.HookGen");
            AsmHookGen = LazyLoadAssembly(Path.Combine(PathGame, "MonoMod.RuntimeDetour.HookGen.exe"));
        }

        public static void RunMonoMod(string asmFrom, string asmTo = null) {
            if (asmTo == null)
                asmTo = asmFrom;
            LogLine($"Running MonoMod for {asmFrom}");
            // We're lazy.
            Environment.SetEnvironmentVariable("MONOMOD_DEPDIRS", PathGame);
            Environment.SetEnvironmentVariable("MONOMOD_MODS", PathGame);
            Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "0");
            AsmMonoMod.EntryPoint.Invoke(null, new object[] { new string[] { asmFrom, asmTo + ".tmp" } });

            if (!File.Exists(asmTo + ".tmp"))
                throw new Exception("MonoMod failed creating a patched assembly!");

            if (File.Exists(asmTo))
                File.Delete(asmTo);
            File.Move(asmTo + ".tmp", asmTo);
        }

        public static void RunHookGen(string asm) {
            LogLine($"Running MonoMod.RuntimeDetour.HookGen for {asm}");
            // We're lazy.
            Environment.SetEnvironmentVariable("MONOMOD_DEPDIRS", PathGame);
            Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "0");
            AsmHookGen.EntryPoint.Invoke(null, new object[] { new string[] { "--private", asm } });
        }

        public static void StartGame() {
            LogLine("Restarting Celeste");
            Process game = new Process();
            // If the game was installed via Steam, it should restart in a Steam context on its own.
            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX) {
                // The Linux and macOS versions come with a wrapping bash script.
                game.StartInfo.FileName = "bash";
                game.StartInfo.Arguments = "\"" + PathCelesteExe.Substring(0, PathCelesteExe.Length - 4) + "\"";
            } else {
                game.StartInfo.FileName = PathCelesteExe;
            }
            game.StartInfo.WorkingDirectory = PathGame;
            game.Start();
        }


        // AFAIK there's no "clean" way to check for any file locks in C#.
        static bool CanReadWrite(string path) {
            try {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
                    return true;
            } catch {
                return false;
            }
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

    }
}
