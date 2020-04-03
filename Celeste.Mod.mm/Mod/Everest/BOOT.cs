using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using SDL2;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Celeste.Mod {
    /// <summary>
    /// RUN AWAY. TURN AROUND. GO TO CELESTE'S MAIN FUNCTION INSTEAD.
    /// </summary>
    internal static class BOOT {

        [MakeEntryPoint]
        private static void Main(string[] args) {
            try {
                string everestPath = typeof(Celeste).Assembly.Location;

                if (args.FirstOrDefault() == "--vanilla")
                    goto StartVanilla;

                if (args.FirstOrDefault() == "--no-appdomain") {
                    Console.WriteLine("Loading Everest, no AppDomain");
                    patch_Celeste.Main(args);
                    return;
                }

                if (!AppDomain.CurrentDomain.IsDefaultAppDomain()) {
                    patch_Celeste.Main(args);
                    AppDomain.CurrentDomain.SetData("EverestRestartVanilla", Everest.RestartVanilla);
                    return;
                }

                // This gets skipped when directly starting into vanilla mode or when restarting into vanilla.
                // Sadly returning from vanilla to Everest is impossible as vanilla forcibly kills the process outside of Windows.

                Console.WriteLine("Loading Everest");
                using (AppDomainWrapper adw = new AppDomainWrapper("Everest", out bool[] status)) {
                    AppDomain ad = adw.AppDomain;

                    ThreadIfNeeded("EVEREST", () => ad.ExecuteAssembly(everestPath, args));

                    if (ad.GetData("EverestRestartVanilla") as bool? ?? false) {
                        for (int i = 0; i < 5; i++) {
                            adw.Dispose();
                            if (!status[0])
                                Thread.Sleep(1000);
                        }

                        if (!status[0])
                            throw new Exception("Cannot unload Everest. Please restart Celeste using --vanilla if needed.");

                        goto StartVanilla;
                    }

                    return;
                }


                StartVanilla:
                using (AppDomainWrapper adw = new AppDomainWrapper("Celeste", out bool[] status)) {
                    Console.WriteLine("Loading Vanilla");
                    AppDomain ad = adw.AppDomain;

                    string vanillaPath = Path.Combine(Path.GetDirectoryName(everestPath), "orig", "Celeste.exe");
                    string loaderPath = Path.Combine(Path.GetDirectoryName(everestPath), "EverestVanillaLoader.dll");

                    if (File.Exists(loaderPath))
                        File.Delete(loaderPath);

                    // Separate the EverestVanillaLoader class into its own assembly.
                    // This is needed to not load Everest's Celeste.exe by accident.
                    string calFullName;
                    using (ModuleDefinition wrapper = ModuleDefinition.CreateModule("EverestVanillaLoader", new ModuleParameters() {
                        ReflectionImporterProvider = MMReflectionImporter.Provider
                    }))
                    using (EverestVanillaLoaderMonoModder mm = new EverestVanillaLoaderMonoModder() {
                        Module = wrapper,
                        CleanupEnabled = false,
                        MissingDependencyThrow = false
                    }) {
                        mm.WriterParameters.WriteSymbols = false;
                        mm.WriterParameters.SymbolWriterProvider = null;

                        mm.MapDependency(mm.Module, "Celeste");
                        if (!mm.DependencyCache.TryGetValue("Celeste", out ModuleDefinition celeste))
                            throw new FileNotFoundException("Celeste not found!");

                        TypeDefinition orig = mm.Orig = mm.FindType("Celeste.Mod.BOOT/EverestVanillaLoader").Resolve();
                        orig.DeclaringType = null;
                        orig.IsNestedPublic = false;
                        orig.IsPublic = true;
                        orig.Namespace = "Celeste.Mod";
                        calFullName = orig.FullName;

                        wrapper.Architecture = celeste.Architecture;
                        wrapper.Runtime = celeste.Runtime;
                        wrapper.RuntimeVersion = celeste.RuntimeVersion;
                        wrapper.Attributes = celeste.Attributes;
                        wrapper.Characteristics = celeste.Characteristics;
                        wrapper.Kind = ModuleKind.Dll;

                        mm.PrePatchType(orig, forceAdd: true);
                        mm.PatchType(orig);
                        mm.PatchRefs();
                        mm.Write(null, loaderPath);
                    };

                    try {
                        // Move the current Celeste.exe away so that it won't get loaded by accident.
                        if (File.Exists(everestPath + "_"))
                            File.Delete(everestPath + "_");
                        File.Move(everestPath, everestPath + "_");

                        // Can't do this as it'd permanently change Assembly.GetEntryAssembly()
                        // ad.ExecuteAssembly(loaderPath, new string[] { everestPath, vanillaPath, typeof(Celeste).Assembly.FullName });

                        // Must use reflection as the separated type is different from doing typeof(EverestVanillaLoader) here.
                        Type cal = ad.Load("EverestVanillaLoader").GetType(calFullName);
                        ad.SetData("EverestPath", everestPath);
                        ad.SetData("VanillaPath", vanillaPath);
                        ad.SetData("CelesteName", typeof(Celeste).Assembly.FullName);
                        ad.DoCallBack(cal.GetMethod("Run").CreateDelegate(typeof(CrossAppDomainDelegate)) as CrossAppDomainDelegate);

                    } finally {
                        if (File.Exists(everestPath + "_"))
                            File.Move(everestPath + "_", everestPath);
                    }

                    // Luckily the newly loaded vanilla Celeste.exe becomes the executing assembly from now on.
                    ThreadIfNeeded("VANILLA", () => ad.ExecuteAssembly(vanillaPath, args));

                    return;
                }

            } catch (Exception e) {
                LogError("BOOT-CRITICAL", e);
            }
        }

        public static void LogError(string tag, Exception e) {
            e.LogDetailed(tag);
            try {
                ErrorLog.Write(e.ToString());
                ErrorLog.Open();
            } catch { }
        }

        public static void ThreadIfNeeded(string tag, ThreadStart start) {
            ThreadStart wrap = () => {
                try {
                    start();
                } catch (Exception e) {
                    LogError(tag, e);
                }
            };

            if (PlatformHelper.Is(MonoMod.Utils.Platform.MacOS)) {
                // macOS must run the game on the main thread. Otherwise this happens:
                // https://cdn.discordapp.com/attachments/429775439423209472/694105211802746940/dump.txt
                wrap();
                return;
            }

            // Win32 requires a separate thread for a separate message queue / pump.
            // Linux might benefit from additional thread local isolation.
            Thread t = new Thread(wrap);
            t.Start();
            t.Join();
        }

        // Utility MonoModder responsible for separating EverestVanillaLoader into its own assembly.
        internal class EverestVanillaLoaderMonoModder : MonoModder {
            public TypeDefinition Orig;

            public override void Log(string text) {
            }

            public override IMetadataTokenProvider Relinker(IMetadataTokenProvider mtp, IGenericParameterProvider context) {
                if (mtp is TypeReference && ((TypeReference) mtp).FullName == Orig.FullName) {
                    return Module.GetType(Orig.FullName);
                }
                return base.Relinker(mtp, context);
            }
        }

        // This class will be separated into its own assembly just to force-load the original Celeste.exe in the vanilla AppDomain.
        public static class EverestVanillaLoader {
            static string EverestPath = AppDomain.CurrentDomain.GetData("EverestPath") as string;
            static string VanillaPath = AppDomain.CurrentDomain.GetData("VanillaPath") as string;
            static string CelesteName = AppDomain.CurrentDomain.GetData("CelesteName") as string;
            static Assembly CelesteAsm;

            public static void Run() {
                AppDomain ad = AppDomain.CurrentDomain;
                ad.AssemblyResolve += AssemblyResolver;
                ad.TypeResolve += TypeResolver;
                CelesteAsm = Assembly.Load(CelesteName);
                Console.WriteLine($"EverestVanillaLoader loaded Celeste from: {CelesteAsm.Location}");

                // The loaded Celeste.exe is in the orig subfolder.
                // Sadly Monocle assumes the entry executable to be in the same folder as the content dir.
                // Luckily we can hackfix that.

                // Sadly we can't set the value or even try-catch RuntimeHelpers.RunClassConstructor(CelesteAsm.GetType("Monocle.Engine").TypeHandle)
                // Throwing class constructors will just rerun until they don't throw.

                // Furthermore, we can't just set a magic field, at least not on mono.
                // In .NET, Assembly.GetEntryAssembly() -> AppDomainManager.EntryAssembly with a cached field
                // In mono, AppDomainManager.EntryAssembly -> Assembly.GetEntryAssembly(), internal call
                // Let's do the ugliest thing ever: native detour .NET

                using (NativeDetour pleaseEndMeAlreadyWhatDidIDoToDeserveThis = new NativeDetour(
                    typeof(Assembly).GetMethod("GetEntryAssembly"),
                    typeof(EverestVanillaLoader).GetMethod("GetEntryAssembly")
                )) {
                    CelesteAsm
                        .GetType("Monocle.Engine")
                        .GetField("AssemblyDirectory", BindingFlags.NonPublic | BindingFlags.Static)
                        .SetValue(null, Path.GetDirectoryName(EverestPath));

                    // Similar restrictions apply to XNA's TitleLocation.Path, which is reimplemented accurately by FNA.
                    // Sadly, XNA uses GetEntryAssembly as well.
                    // Luckily, some basic reflection exposed a (presumably cache) field that doesn't exist in FNA.
                    typeof(TitleContainer).Assembly
                        .GetType("Microsoft.Xna.Framework.TitleLocation")
                        ?.GetField("_titleLocation", BindingFlags.NonPublic | BindingFlags.Static)
                        ?.SetValue(null, Path.GetDirectoryName(EverestPath));
                }
            }

            public static Assembly AssemblyResolver(object sender, ResolveEventArgs args) {
                if ((CelesteAsm != null && args.Name == CelesteAsm.FullName) || new AssemblyName(args.Name).Name == "Celeste")
                    return CelesteAsm ?? (CelesteAsm = Assembly.LoadFrom(VanillaPath));
                return null;
            }

            public static Assembly TypeResolver(object sender, ResolveEventArgs args) {
                if (args.Name == CelesteAsm.FullName || new AssemblyName(args.Name).Name == "Celeste")
                    return CelesteAsm.GetType(args.Name) != null ? CelesteAsm : null;
                return null;
            }

            public static Assembly GetEntryAssembly() {
                return CelesteAsm;
            }
        }

        // Basic app domain wrapper helper.
        public class AppDomainWrapper : IDisposable {
            public AppDomain AppDomain;
            private readonly bool[] _Status;
            private readonly Dictionary<string, string> _EnvironmentVariables;

            public AppDomainWrapper(string suffix, out bool[] status) {
                AppDomain pad = AppDomain.CurrentDomain;
                AppDomain = AppDomain.CreateDomain($"{pad.FriendlyName ?? "Celeste"}+{suffix}", null, new AppDomainSetup() {
                    ApplicationBase = pad.BaseDirectory,
                    LoaderOptimization = LoaderOptimization.SingleDomain
                });

                _Status = status = new bool[1];

                _EnvironmentVariables = _GetEnvironmentVariables();
                _SetEnvironmentVariables();
            }

            private Dictionary<string, string> _GetEnvironmentVariables() {
                Dictionary<string, string> env = new Dictionary<string, string>();
                foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                    env[(string) entry.Key] = (string) entry.Value;
                return env;
            }

            private void _SetEnvironmentVariables() {
                foreach (string key in _GetEnvironmentVariables().Keys) {
                    if (!_EnvironmentVariables.ContainsKey(key)) {
                        // Console.WriteLine($"SET \"{key}\" = NULL");
                        Environment.SetEnvironmentVariable(key, null);
                    }
                }

                foreach (KeyValuePair<string, string> entry in _EnvironmentVariables) {
                    // Console.WriteLine($"SET \"{entry.Key}\" = \"{entry.Value}\"");
                    Environment.SetEnvironmentVariable(entry.Key, entry.Value);
                }
            }

            public void Dispose() {
                if (AppDomain == null)
                    return;

                string name = AppDomain.FriendlyName;

                try {
                    AppDomain.Unload(AppDomain);
                    AppDomain = null;
                    _Status[0] = true;
                    Console.WriteLine($"Unloaded AppDomain {name}");

                    GC.Collect();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    _SetEnvironmentVariables();

                    _ResetXFNA();

                } catch (CannotUnloadAppDomainException e) {
                    _Status[0] = false;
                    Console.WriteLine($"COULDN'T UNLOAD APPDOMAIN {name}");
                    Console.WriteLine(e);
                }
            }

            [MonoModIgnore]
            private static extern void _ResetXFNA();

            [MonoModIfFlag("XNA")]
            [MonoModPatch("_ResetXFNA")]
            [MonoModReplace]
            private static void _ResetXNA() {
                // No resetting needed on XNA.
            }

            [MonoModIfFlag("FNA")]
            [MonoModPatch("_ResetXFNA")]
            [MonoModReplace]
            private static void _ResetFNA() {
                // GL attributes don't get reset, meaning that creating a GLES context in FNA once
                // will make all following contexts GLES by default.

                // There's a slight chance that OpenGL isn't even the current render.
                // But that shouldn't cause any issues, as SDL2 always gets built with GL support... right?
                SDL.SDL_GL_ResetAttributes();
            }
        }

    }
}
