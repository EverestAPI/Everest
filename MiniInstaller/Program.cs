using Celeste.Mod.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;

namespace MiniInstaller {
    public class Program {

        public static string PathUpdate;
        public static string PathGame;
        public static string PathCelesteExe;
        public static string PathEverestExe;
        public static string PathOrig;
        public static string PathDylibs;
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

                return Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(PathUpdate), asmName.Name + ".dll"));
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

                    if (!IsMonoVersionCompatible()) {
                        LogLine("Everest installer only works with Mono 5 and higher.");
                        LogLine("Please upgrade Mono and run the installer again.");
                        throw new Exception("Incompatible Mono version");
                    }

                    WaitForGameExit();

                    Backup();

                    MoveFilesFromUpdate();

                    MoveDylibs();

                    if (AsmMonoMod == null) {
                        LoadMonoMod();
                    }
                    RunMonoMod(Path.Combine(PathOrig, "Celeste.exe"), PathEverestExe, new string[] { Path.ChangeExtension(PathCelesteExe, ".Mod.mm.dll") });
                    RunHookGen(PathEverestExe, PathCelesteExe);
                    MakeLargeAddressAware(PathEverestExe);
                    CombineXMLDoc(Path.ChangeExtension(PathCelesteExe, ".Mod.mm.xml"), Path.ChangeExtension(PathCelesteExe, ".xml"));


                    // If we're updating, start the game. Otherwise, close the window. 
                    if (PathUpdate != null) {
                        StartGame();
                    }

                } catch (Exception e) {
                    string msg = e.ToString();
                    LogLine("");
                    LogLine(msg);
                    LogLine("");
                    LogLine("Installing Everest failed.");
                    if (msg.Contains("MonoMod failed relinking Microsoft.Xna.Framework")) {
                        LogLine("Please run the game at least once to install missing dependencies.");
                    } else {
                        if (msg.Contains("--->")) {
                            LogLine("Please review the error after the '--->' to see if you can fix it on your end.");
                        }
                        LogLine("");
                        LogLine("If you need help, please create a new issue on GitHub @ https://github.com/EverestAPI/Everest");
                        LogLine("or join the #modding_help channel on Discord (invite in the repo).");
                        LogLine("Make sure to upload your log file.");
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

        public static bool IsMonoVersionCompatible() {
            // Outdated Mono versions can corrupt Celeste.exe when patching.
            // (see https://github.com/EverestAPI/Everest/issues/62)

            try {
                // Mono version detection code: https://stackoverflow.com/a/4180030
                Type monoRuntimeType = Type.GetType("Mono.Runtime");
                if (monoRuntimeType != null) {
                    MethodInfo getDisplayNameMethod = monoRuntimeType.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
                    if (getDisplayNameMethod != null) {
                        string version = (string) getDisplayNameMethod.Invoke(null, null);
                        // version should look like this: "6.8.0.123 (tarball Tue May 12 15:13:37 UTC 2020)"
                        int majorVersion = int.Parse(version.Split('.')[0]);

                        // major 5 should work while people have issues with major 4
                        if (majorVersion < 5) {
                            return false;
                        }
                    }
                }
                // if the runtime isn't Mono or if Mono isn't too old, it should be compatible
                return true;
            } catch (Exception) {
                // ignore exception and continue, we don't want to block users if "GetDisplayName" changes
                LogLine("Could not determine Mono version.");
                LogLine("Everest installer works with Mono 5 and higher.");
                LogLine("Please see https://github.com/EverestAPI/Everest/issues/62 if you run into issues.");
                return true;
            }
        }

        public static bool SetupPaths() {
            PathGame = Directory.GetCurrentDirectory();
            Console.WriteLine(PathGame);

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
                return false;
            }

            // Here lies a reminder that patching into Everest.exe only caused confusion and issues.
            // RIP Everest.exe 2019 - 2020
            PathEverestExe = PathCelesteExe;

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
            if (!CanReadWrite(PathEverestExe)) {
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
            }

            Backup(PathCelesteExe);
            Backup(PathCelesteExe + ".pdb");
            Backup(Path.ChangeExtension(PathCelesteExe, "mdb"));
            Backup(PathCelesteExe + ".config");
        }

        public static void Backup(string from) {
            string to = Path.Combine(PathOrig, Path.GetFileName(from));
            if (File.Exists(from) && !File.Exists(to)) {
                LogLine($"Backing up {from} => {to}");
                File.Copy(from, to);
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

        public static void MoveDylibs() {
            if (PathDylibs != null) {
                LogLine("Moving native libraries");
                foreach (string fileGame in Directory.GetFiles(PathGame)) {
                    if (!fileGame.EndsWith(".dylib"))
                        continue;
                    string fileRelative = fileGame.Substring(PathGame.Length + 1);
                    string fileDylibs = Path.Combine(PathDylibs, fileRelative);
                    LogLine($"Copying {fileGame} +> {fileDylibs}");
                    File.Copy(fileGame, fileDylibs, true);
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

        public static void RunMonoMod(string asmFrom, string asmTo = null, string[] dllPaths = null) {
            asmTo ??= asmFrom;
            dllPaths ??= new string[] { PathGame };

            LogLine($"Running MonoMod for {asmFrom}");
            // We're lazy.
            Environment.SetEnvironmentVariable("MONOMOD_DEPDIRS", PathGame);
            Environment.SetEnvironmentVariable("MONOMOD_MODS", string.Join(Path.PathSeparator.ToString(), dllPaths));
            Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "0");
            AsmMonoMod.EntryPoint.Invoke(null, new object[] { new string[] { asmFrom, asmTo + ".tmp" } });

            if (!File.Exists(asmTo + ".tmp"))
                throw new Exception("MonoMod failed creating a patched assembly!");

            if (File.Exists(asmTo))
                File.Delete(asmTo);
            File.Move(asmTo + ".tmp", asmTo);
        }

        public static void RunHookGen(string asm, string target) {
            LogLine($"Running MonoMod.RuntimeDetour.HookGen for {asm}");
            // We're lazy.
            Environment.SetEnvironmentVariable("MONOMOD_DEPDIRS", PathGame);
            Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "0");
            AsmHookGen.EntryPoint.Invoke(null, new object[] { new string[] { "--private", asm, Path.Combine(Path.GetDirectoryName(target), "MMHOOK_" + Path.ChangeExtension(Path.GetFileName(target), "dll")) } });
        }

        // Based on https://github.com/RomSteady/RomTerraria/blob/c017139c54b82fa86c1e645be5b51656e637d449/RTRewriter/Cecil/Rewriter.cs#L37
        public static void MakeLargeAddressAware(string asm) {
            // https://docs.microsoft.com/en-us/windows/desktop/debug/pe-format#characteristics
            const ushort IMAGE_FILE_LARGE_ADDRESS_AWARE = 0x0020;
            using (FileStream stream = File.Open(asm, FileMode.Open, FileAccess.ReadWrite))
            using (BinaryReader reader = new BinaryReader(stream))
            using (BinaryWriter writer = new BinaryWriter(stream)) {
                // Check for MZ header.
                if (reader.ReadInt16() != 0x5A4D)
                    return;

                // Skip to the PE header, then check for it.
                reader.BaseStream.Position = 0x3C;
                reader.BaseStream.Position = reader.ReadInt32();
                if (reader.ReadInt32() != 0x4550)
                    return;

                // Go to the "characteristics" in the header.
                reader.BaseStream.Position += 0x12;

                // Set the IMAGE_FILE_LARGE_ADDRESS_AWARE flag if not already set.
                long pos = reader.BaseStream.Position;
                ushort flags = reader.ReadUInt16();
                if ((flags & IMAGE_FILE_LARGE_ADDRESS_AWARE) == IMAGE_FILE_LARGE_ADDRESS_AWARE)
                    return;

                flags |= IMAGE_FILE_LARGE_ADDRESS_AWARE;

                writer.Seek((int) pos, SeekOrigin.Begin);
                writer.Write(flags);
                writer.Flush();
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
                // The Linux and macOS versions come with a wrapping bash script.
                game.StartInfo.FileName = PathEverestExe.Substring(0, PathEverestExe.Length - 4);
                if (!File.Exists(game.StartInfo.FileName))
                    game.StartInfo.FileName = PathCelesteExe.Substring(0, PathCelesteExe.Length - 4);
                // 1.3.3.0 splits Celeste into two, so to speak.
                if (!File.Exists(game.StartInfo.FileName) && Path.GetFileName(PathCelesteExe) == "Celeste.exe" && Path.GetFileName(Path.GetDirectoryName(PathCelesteExe)) == "Resources")
                    game.StartInfo.FileName = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(PathCelesteExe)), "MacOS", "Celeste");
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
                LogLine("Documentation combining aborted.");
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
