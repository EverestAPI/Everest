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

                    BackupOrRestore();

                    MoveFilesFromUpdate();

                    if (AsmMonoMod == null)
                        AsmMonoMod = LoadMonoMod();
                    RunMonoMod(AsmMonoMod);

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
                PathUpdate = Path.GetFullPath(".");
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
                LogLine("Celeste still running - waiting");
                while (!CanReadWrite(PathCelesteExe))
                    Thread.Sleep(100);
            }
        }

        public static void BackupOrRestore() {
            // Backup / restore the original game files we're going to modify.
            // TODO: Maybe invalidate the orig dir when the backed up version < installed version?
            if (!Directory.Exists(PathOrig)) {
                LogLine("Creating backup orig directory");
                Directory.CreateDirectory(PathOrig);
                File.Copy(PathCelesteExe, Path.Combine(PathOrig, "Celeste.exe"));

            } else {
                LogLine("Restoring files from orig directory");
                File.Copy(Path.Combine(PathOrig, "Celeste.exe"), PathCelesteExe, true);
            }
        }

        public static void MoveFilesFromUpdate() {
            if (PathUpdate != null) {
                LogLine("Moving files from update directory");
                foreach (string fileUpdate in Directory.GetFiles(PathUpdate)) {
                    string fileRelative = fileUpdate.Substring(PathUpdate.Length + 1);
                    string fileGame = Path.Combine(PathGame, fileRelative);
                    if (File.Exists(fileGame)) {
                        LogLine($"Deleting existing {fileGame}");
                        File.Delete(fileGame);
                    }
                    // We can't move MiniInstaller.exe while it's running.
                    if (fileRelative == "MiniInstaller.exe") {
                        // Copy it instead.
                        LogLine($"{fileUpdate} +> {fileGame}");
                        File.Copy(fileUpdate, fileGame);
                    } else {
                        // Move all other files, though.
                        LogLine($"{fileUpdate} -> {fileGame}");
                        File.Move(fileUpdate, fileGame);
                    }
                }
            }
        }

        public static Assembly LoadMonoMod() {
            // We can't add MonoMod as a reference to MiniInstaller, as we don't want to accidentally lock the file.
            // Instead, load it dynamically and invoke the entry point.
            LogLine("Loading MonoMod");
            // We also need to lazily load any dependencies.
            LazyLoadAssembly(Path.Combine(PathGame, "Mono.Cecil.dll"));
            LazyLoadAssembly(Path.Combine(PathGame, "Mono.Cecil.Mdb.dll"));
            LazyLoadAssembly(Path.Combine(PathGame, "Mono.Cecil.Pdb.dll"));
            Assembly asmMonoMod = LazyLoadAssembly(Path.Combine(PathGame, "MonoMod.exe"));
            return asmMonoMod;
        }

        public static void RunMonoMod(Assembly asmMonoMod) {
            // We're lazy.
            asmMonoMod.EntryPoint.Invoke(null, new object[] { new string[] { PathCelesteExe } });

            LogLine("Replacing Celeste.exe");
            File.Delete(PathCelesteExe);
            File.Move(Path.Combine(PathGame, "MONOMODDED_Celeste.exe"), PathCelesteExe);
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
            Assembly asm = Assembly.LoadFrom(path);
            AppDomain.CurrentDomain.TypeResolve += (object sender, ResolveEventArgs args) => {
                return asm.GetType(args.Name) != null ? asm : null;
            };
            AppDomain.CurrentDomain.AssemblyResolve += (object sender, ResolveEventArgs args) => {
                return args.Name == asm.FullName || args.Name == asm.GetName().Name ? asm : null;
            };
            return asm;
        }

    }
}
