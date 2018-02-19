using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Celeste.Mod;
using System.Reflection;
using System.Diagnostics;
using System.Threading;

namespace MiniInstaller {
    class Program {

        // Fields instead of local variables for when we're going to split up this monstrosity into multiple methods.
        static string PathUpdate;
        static string PathGame;
        static string PathCelesteExe;
        static string PathOrig;
        static string PathLog;

        static void Main(string[] args) {
            Console.WriteLine("Everest MiniInstaller");

            // Set up / determine any paths.

            PathGame = Path.GetFullPath(".");

            if (Path.GetFileName(PathGame) == "everest-update" &&
                File.Exists(Path.Combine(Path.GetDirectoryName(PathGame), "Celeste.exe"))) {
                // We're updating Everest via the in-game installler.
                PathUpdate = Path.GetFullPath(".");
                PathGame = Path.GetDirectoryName(PathUpdate);
            }

            PathCelesteExe = Path.Combine(PathGame, "Celeste.exe");
            if (!File.Exists(PathCelesteExe)) {
                Console.WriteLine("Celeste.exe not found!");
                Console.WriteLine("Did you extract the .zip into the same place as Celeste?");
                Console.WriteLine();
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                return;
            }

            if (
                (PathUpdate == null && !File.Exists(Path.Combine(PathGame, "Celeste.Mod.mm.dll"))) ||
                (PathUpdate != null && !File.Exists(Path.Combine(PathUpdate, "Celeste.Mod.mm.dll")))
            ) {
                Console.WriteLine("Celeste.Mod.mm.dll not found!");
                Console.WriteLine("Did you extract all the files in the .zip?");
                Console.WriteLine();
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                return;
            }

            PathOrig = Path.Combine(PathGame, "orig");
            PathLog = Path.Combine(PathGame, "miniinstaller-log.txt");

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

                    if (!CanReadWrite(PathCelesteExe)) {
                        Console.WriteLine("Celeste still running - waiting");
                        while (!CanReadWrite(PathCelesteExe))
                            Thread.Sleep(100);
                    }

                    // Backup / restore the original game files we're going to modify.
                    // TODO: Maybe invalidate the orig dir when the backed up version < installed version?
                    if (!Directory.Exists(PathOrig)) {
                        Console.WriteLine("Creating backup orig directory");
                        Directory.CreateDirectory(PathOrig);
                        File.Copy(PathCelesteExe, Path.Combine(PathOrig, "Celeste.exe"));

                    } else {
                        Console.WriteLine("Restoring files from orig directory");
                        File.Copy(Path.Combine(PathOrig, "Celeste.exe"), PathCelesteExe, true);
                    }

                    if (PathUpdate != null) {
                        Console.WriteLine("Moving files from update directory");
                        foreach (string fileUpdate in Directory.GetFiles(PathUpdate)) {
                            string fileRelative = fileUpdate.Substring(PathUpdate.Length + 1);
                            string fileGame = Path.Combine(PathGame, fileRelative);
                            if (File.Exists(fileGame)) {
                                Console.WriteLine($"Deleting existing {fileGame}");
                                File.Delete(fileGame);
                            }
                            // We can't move MiniInstaller.exe while it's running.
                            if (fileRelative == "MiniInstaller.exe") {
                                // Copy it instead.
                                Console.WriteLine($"{fileUpdate} +> {fileGame}");
                                File.Copy(fileUpdate, fileGame);
                            } else {
                                // Move all other files, though.
                                Console.WriteLine($"{fileUpdate} -> {fileGame}");
                                File.Move(fileUpdate, fileGame);
                            }
                        }
                    }

                    // We can't add MonoMod as a reference to MiniInstaller, as we don't want to accidentally lock the file.
                    // Instead, load it dynamically and invoke the entry point.
                    Console.WriteLine("Loading MonoMod");
                    // We also need to lazily load any dependencies.
                    LazyLoadAssembly(Path.Combine(PathGame, "Mono.Cecil.dll"));
                    LazyLoadAssembly(Path.Combine(PathGame, "Mono.Cecil.Mdb.dll"));
                    LazyLoadAssembly(Path.Combine(PathGame, "Mono.Cecil.Pdb.dll"));
                    Assembly asmMonoMod = LazyLoadAssembly(Path.Combine(PathGame, "MonoMod.exe"));

                    asmMonoMod.EntryPoint.Invoke(null, new object[] { new string[] { PathCelesteExe } });

                    Console.WriteLine("Replacing Celeste.exe");
                    File.Delete(PathCelesteExe);
                    File.Move(Path.Combine(PathGame, "MONOMODDED_Celeste.exe"), PathCelesteExe);

                    if (!Directory.Exists(Path.Combine(PathGame, "Mods"))) {
                        Console.WriteLine("Creating Mods directory");
                        Directory.CreateDirectory(Path.Combine(PathGame, "Mods"));
                    }

                } catch (Exception e) {
                    Console.WriteLine();
                    Console.WriteLine(e.ToString());
                    Console.WriteLine();
                    Console.WriteLine("Installing Everest failed.");
                    Console.WriteLine("Please create a new issue on GitHub @ https://github.com/EverestAPI/Everest");
                    Console.WriteLine("or join the #game_modding channel on Discord (invite in the repo).");
                    Console.WriteLine("Make sure to upload your miniinstaller-log.txt");
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadKey();
                    return;
                }

                Console.SetOut(logWriter.STDOUT);
                logWriter.STDOUT = null;
            }

            // If we're updating, start the game. Otherwise, tell the user to close the window. 
            if (PathUpdate != null) {
                Process game = new Process();
                // If the game was installed via Steam, it should restart in a Steam context on its own.
                if (Type.GetType("Mono.Runtime") != null) {
                    game.StartInfo.FileName = "mono";
                    game.StartInfo.Arguments = PathCelesteExe;
                } else {
                    game.StartInfo.FileName = PathCelesteExe;
                }
                game.StartInfo.WorkingDirectory = PathGame;
                game.Start();

            } else {
                Console.WriteLine();
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
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
