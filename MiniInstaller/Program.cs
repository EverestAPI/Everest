using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MiniInstaller {
    public static partial class Program {
        public static int Main(string[] args) {
            // Forward to MonoModRules
            AppDomain.CurrentDomain.SetData("Everest_IsHeadless", args.Contains("headless"));

            if (args.Length == 0) return StandardMode(args);
            if (args[0] == "--fastmode") return FastMode(args);
            return StandardMode(args);
        }

        public static bool Init() {
            if (Type.GetType("Mono.Runtime") != null) {
                Console.WriteLine("MiniInstaller is unable to run under mono!");
                return false;
            }

            // Set working directory
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);

            if (!Globals.SetupPaths()) {
                // setting up paths failed (Celeste.exe was not found).
                return false;
            }
            
            Globals.DetermineInstallPlatform();

            // .NET hates it when strong-named dependencies get updated.
            AppDomain.CurrentDomain.AssemblyResolve += (asmSender, asmArgs) => {
                AssemblyName asmName = new AssemblyName(asmArgs.Name);
                if (!asmName.Name.StartsWith("Mono.Cecil"))
                    return null;

                Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(other => other.GetName().Name == asmName.Name);
                if (asm != null)
                    return asm;

                if (Globals.PathUpdate != null)
                    return Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(Globals.PathUpdate), asmName.Name + ".dll"));

                return null;
            };

            return true;
        }

        public static int StandardMode(string[] args) {
            if (!Init()) return 1;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                if (WindowsElevationRequest.HandlePostElevationBackup(args)) return 0;
            }

            Console.WriteLine("Everest MiniInstaller");

            using Logger.DisposableTuple _ = Logger.SetupLogger();
            try {
                InGameUpdaterHelper.WaitForGameExit();
                InGameUpdaterHelper.EnsureGameIsWriteable();

                BackUp.Backup();

                InGameUpdaterHelper.MoveFilesFromUpdate();

                if (Globals.Platform == Globals.InstallPlatform.MacOS && !File.Exists(Path.Combine(Globals.PathGame, "Celeste.png")))
                    File.Move(Path.Combine(Globals.PathGame, "Celeste-icon.png"), Path.Combine(Globals.PathGame, "Celeste.png"));
                else
                    File.Delete(Path.Combine(Globals.PathGame, "Celeste-icon.png"));

                LibAndDepHandling.DeleteSystemLibs();
                LibAndDepHandling.SetupNativeLibs();
                LibAndDepHandling.CopyControllerDB();

                if (Directory.Exists(Globals.PathMiniInstallerWorkspace)) {
                    Logger.LogLine("MiniInstaller workspace already exists, cleaning before continuing.");
                    Directory.Delete(Globals.PathMiniInstallerWorkspace, recursive: true);
                }

                Directory.CreateDirectory(Globals.PathMiniInstallerWorkspace);

                string moddedCeleste = Path.Combine(Globals.PathMiniInstallerWorkspace, "Celeste.dll");
                string moddedFNA = Path.Combine(Globals.PathMiniInstallerWorkspace, "FNA.dll");
                string hookGenTempOutput = Path.Combine(Globals.PathMiniInstallerWorkspace, "MMHOOK_" + Path.ChangeExtension(Path.GetFileName(Globals.PathCelesteExe), ".dll"));
                string tempAppHost = Path.ChangeExtension(moddedCeleste, ".exe");

                DepCalls.LoadModders();

                // DepCalls.ConvertToNETCore also converts dependencies, so FNA will also be copied to the workspace
                DepCalls.ConvertToNETCore(Path.Combine(Globals.PathOrig, "Celeste.exe"), moddedCeleste);

                string everestModDLL = Path.ChangeExtension(Globals.PathCelesteExe, ".Mod.mm.dll");
                string[] mods = new string[] { Globals.PathEverestLib, everestModDLL };

                DepCalls.RunMonoMod(moddedFNA, dllPaths: mods); // We need to patch some methods in FNA as well
                DepCalls.RunMonoMod(moddedCeleste, dllPaths: mods);

                DepCalls.RunHookGen(moddedCeleste, moddedCeleste);
                DepCalls.RunMonoMod(hookGenTempOutput, dllPaths: mods); // We need to fix some MonoMod crimes, so relink it against the legacy MonoMod layer

                string tempEverestXml = Path.Combine(Globals.PathMiniInstallerWorkspace, "Celeste.Mod.mm.xml");
                string tempCelesteXml = Path.Combine(Globals.PathMiniInstallerWorkspace, "Celeste.xml");
                File.Copy(Path.ChangeExtension(Globals.PathCelesteExe, ".Mod.mm.xml")!, tempEverestXml, overwrite: true);
                File.Copy(Path.ChangeExtension(Globals.PathCelesteExe, ".xml")!, tempCelesteXml, overwrite: true);
                XmlDoc.CombineXMLDoc(tempEverestXml, tempCelesteXml);

                // Everything went well, copy files over, prepare the apphost and clean the workspace
                string hookGenOutput = Path.Combine(Globals.PathGame, "MMHOOK_" + Path.ChangeExtension(Path.GetFileName(Globals.PathCelesteExe), ".dll"));
                MiscUtil.MoveExecutable(moddedFNA, Path.Combine(Globals.PathGame, "FNA.dll"));
                MiscUtil.MoveExecutable(hookGenTempOutput, hookGenOutput);
                MiscUtil.MoveExecutable(moddedCeleste, Globals.PathEverestDLL);
                File.Copy(tempCelesteXml, Path.ChangeExtension(Globals.PathCelesteExe, ".xml")!, overwrite: true);
                LibAndDepHandling.CreateRuntimeConfigFiles(Globals.PathEverestDLL, new string[] { everestModDLL, hookGenOutput });
                LibAndDepHandling.SetupAppHosts(Globals.PathCelesteExe, Globals.PathEverestDLL, Globals.PathEverestDLL);

                Directory.Delete(Globals.PathMiniInstallerWorkspace, recursive: true);

                // If we're updating, start the game. Otherwise, close the window.
                if (Globals.PathUpdate != null) {
                    InGameUpdaterHelper.StartGame();
                }

            } catch (Exception e) {
                string msg = e.ToString();
                Logger.LogLine("");
                Logger.LogErr(msg);
                Logger.LogErr("");
                Logger.LogErr("Installing Everest failed.");
                if (msg.Contains("--->")) Logger.LogErr("Please review the error after the '--->' to see if you can fix it on your end.");
                Logger.LogErr("");
                Logger.LogErr("If you need help, please create a new issue on GitHub @ https://github.com/EverestAPI/Everest");
                Logger.LogErr("or join the #modding_help channel on Discord (invite in the repo).");
                Logger.LogErr("Make sure to upload your log file.");
                return 1;

            } finally {
                // Let's not pollute <insert installer name here>.
                Environment.SetEnvironmentVariable("MONOMOD_DEPDIRS", "");
                Environment.SetEnvironmentVariable("MONOMOD_MODS", "");
                Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "");
            }

            return 0;
        }

        /// <summary>
        /// Fast mode serves as a way to speed up development environments,
        /// allowing disabling most parts of the installation process to only focus on the ones
        /// where changes are relevant.
        ///
        /// Its five flags are:
        /// "maingame": Runs MonoMod.Patcher with the Celeste.exe from orig and moves it to the celeste.dll
        /// "fna": Runs MonoMod.Patcher with FNA.dll
        /// "hookgen": Runs MonoMod.HookGen with the present dll, then runs MonoMod.Patcher on it to relink the HEM
        /// "apphost": Only if "maingame" is also present, forces the regeneration of an apphost and runtime config files
        /// "xmldoc": Only if "xmldoc" is also present, combines xmldocs
        /// </summary>
        public static int FastMode(string[] args) {
            bool doMainGame = false;
            bool coreGameCacheRegen = false;
            bool doFNA = false;
            bool doHookGen = false;
            bool doAppHost = false;
            bool doXmlDoc = false;
            if (args.Length == 1) {
                doMainGame = true;
                doFNA = true;
                doHookGen = true;
                doAppHost = true;
                doXmlDoc = true;
            } else {
                doMainGame = args.Contains("maingame");
                coreGameCacheRegen = args.Contains("coreifier-invalidate-cache");
                doFNA = args.Contains("fna");
                doHookGen = args.Contains("hookgen");
                doAppHost = args.Contains("apphost");
                doXmlDoc = args.Contains("xmldoc");
            }

            try {
                if (!Init()) return 1;
                Console.WriteLine("Everest MiniInstaller - FastMode");

                using Logger.DisposableTuple _ = Logger.SetupLogger();

                Globals.DetermineInstallPlatform();

                if (!Directory.Exists(Globals.PathOrig)) {
                    Logger.LogErr("FastMode is unsupported from a fresh installation, run miniinstaller normally first.");
                    return 1;
                }

                DepCalls.LoadModders();

                string everestModDLL = Path.ChangeExtension(Globals.PathCelesteExe, ".Mod.mm.dll");
                string[] mods = new string[] { Globals.PathEverestLib, everestModDLL };

                string coreGameCacheFile = Path.ChangeExtension(Globals.PathCelesteExe, ".CoreGameCache.dll");

                if (doMainGame && !File.Exists(coreGameCacheFile))
                    coreGameCacheRegen = true;

                if (coreGameCacheRegen && File.Exists(coreGameCacheFile))
                    File.Delete(coreGameCacheFile);

                if (coreGameCacheRegen) {
                    // We really only need to coreify celeste
                    DepCalls.ConvertToNETCoreSingle(Path.Combine(Globals.PathOrig, "Celeste.exe"), coreGameCacheFile);
                }

                if (doFNA) {
                    DepCalls.RunMonoMod(Path.Combine(Globals.PathEverestLib, "FNA.dll"), Path.Combine(Globals.PathGame, "FNA.dll"), dllPaths: mods); // We need to patch some methods in FNA as well
                }

                if (doMainGame) {
                    DepCalls.RunMonoMod(coreGameCacheFile, Globals.PathEverestDLL, dllPaths: mods);
                }

                // This should never change no matter the current settings
                string hookGenOutput = Path.Combine(Globals.PathGame, "MMHOOK_" + Path.ChangeExtension(Path.GetFileName(Globals.PathCelesteExe), ".dll"));
                if (doHookGen) {
                    DepCalls.RunHookGen(Globals.PathEverestDLL, Globals.PathCelesteExe);
                    DepCalls.RunMonoMod(hookGenOutput, dllPaths: mods); // We need to fix some MonoMod crimes, so relink it against the legacy MonoMod layer
                }

                if (doMainGame) {
                    // There's usually no reason to do this more than once ever, so don't unless explicitly told
                    // And assembly references changing is also a rare occasion, so skip it as well
                    if (doAppHost) {
                        LibAndDepHandling.CreateRuntimeConfigFiles(Globals.PathEverestDLL, new string[] { everestModDLL, hookGenOutput });
                        LibAndDepHandling.SetupAppHosts(Globals.PathCelesteExe, Globals.PathEverestDLL, Globals.PathEverestDLL);
                    }

                    // Combining xml docs is slow, and most of the time not even required
                    if (doXmlDoc) {
                        XmlDoc.CombineXMLDoc(Path.ChangeExtension(Globals.PathCelesteExe, ".Mod.mm.xml"), Path.ChangeExtension(Globals.PathCelesteExe, ".xml"));
                    }
                }
            } catch (Exception e) {
                string msg = e.ToString();
                Logger.LogLine("");
                Logger.LogErr(msg);
                Logger.LogErr("");
                Logger.LogErr("Installing Everest with FastMode failed.");
                Logger.LogErr($"Settings: ({nameof(doMainGame)}, {nameof(doFNA)}, {nameof(doHookGen)}, {nameof(doAppHost)}) -> ({doMainGame}, {doFNA}, {doHookGen}, {doAppHost})");
                Logger.LogErr("Try rerunning fast mode with more settings enabled, otherwise do a full standard run.");
                return 1;
            } finally {
                Environment.SetEnvironmentVariable("MONOMOD_DEPDIRS", "");
                Environment.SetEnvironmentVariable("MONOMOD_MODS", "");
                Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "");
            }

            return 0;
        }
    }
}
