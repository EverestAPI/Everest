using Celeste.Mod.Helpers;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Celeste.Mod {
    public static partial class Everest {
        /// <summary>
        /// Relink mods to point towards Celeste.exe and FNA / XNA properly and to patch older mods to make them remain compatible.
        /// </summary>
        public static class Relinker {

            /// <summary>
            /// The current Celeste.exe's checksum.
            /// </summary>
            public static string GameChecksum => _GameChecksum = (_GameChecksum ?? Everest.GetChecksum(Assembly.GetAssembly(typeof(Relinker)).Location).ToHexadecimalString());
            private static string _GameChecksum;

            /// <summary>
            /// The lock which the relinker holds when relinking assemblies
            /// </summary>
            public static readonly object RelinkerLock = new object();

            /// <summary>
            /// A map shared between all invocations of the relinker which maps certain referenced modules onto others. Can be used with <see cref="MonoModder.RelinkModuleMap"/>.
            /// </summary>
            public static Dictionary<string, ModuleDefinition> SharedRelinkModuleMap {
                get {
                    if (_SharedRelinkModuleMap != null)
                        return _SharedRelinkModuleMap;

                    _SharedRelinkModuleMap = new Dictionary<string, ModuleDefinition>();

                    // Iterate over all mod assemblies in the game folder
                    foreach (string path in Directory.GetFiles(PathGame)) {
                        string name = Path.GetFileName(path);
                        if (name.EndsWith(".mm.dll")) {
                            string modAsmName = Path.GetFileNameWithoutExtension(path);

                            string relinkedPath;
                            if (name == "Celeste.Mod.mm.dll")
                                // Remap Celeste.Mod.mm.dll to the Celeste executable
                                relinkedPath = typeof(Celeste).Assembly.Location;
                            else {
                                Logger.Warn("relinker", $"Found unexpected mod assembly {name}!");

                                // Remap XYZ.mm.dll to XYZ.dll, if it exists
                                relinkedPath = name.Substring(0, modAsmName.Length - 3);
                                string pathRelinked = Path.Combine(PathGame, relinkedPath + ".dll");    
                                if (File.Exists(pathRelinked))
                                    Logger.Info("relinker", $"-> remapping to {Path.GetFileName(pathRelinked)}");
                                else {
                                    Logger.Info("relinker", $"-> couldn't remap, ignoring...");
                                    continue;
                                }
                            }

                            // Read the module and put it into the map
                            _SharedRelinkModuleMap[modAsmName] = ModuleDefinition.ReadModule(relinkedPath, new ReaderParameters(ReadingMode.Immediate));
                        }
                    }
                    return _SharedRelinkModuleMap;
                }
            }
            private static Dictionary<string, ModuleDefinition> _SharedRelinkModuleMap;

            /// <summary>
            /// A map shared between all invocations of the relinker which maps certain referenced types / methods / fields / etc. onto others. Can be used with <see cref="MonoModder.RelinkMap"/>.
            /// </summary>
            public static Dictionary<string, object> SharedRelinkMap {
                get {
                    if (_SharedRelinkMap != null)
                        return _SharedRelinkMap;

                    _SharedRelinkMap = new Dictionary<string, object>();

                    // Fix old mods depending on MonoModExt
                    _SharedRelinkMap["MonoMod.Utils.MonoModExt"] = "MonoMod.Utils.Extensions";
                    _SharedRelinkMap["System.String MonoMod.Utils.Extensions::GetFindableID(Mono.Cecil.MethodReference,System.String,System.String,System.Boolean,System.Boolean)"] =
                        new RelinkMapEntry("MonoMod.Utils.Extensions", "System.String GetID(Mono.Cecil.MethodReference,System.String,System.String,System.Boolean,System.Boolean)");
                    _SharedRelinkMap["System.String MonoMod.Utils.Extensions::GetFindableID(System.Reflection.MethodBase,System.String,System.String,System.Boolean,System.Boolean,System.Boolean)"] =
                        new RelinkMapEntry("MonoMod.Utils.Extensions", "System.String GetID(System.Reflection.MethodBase,System.String,System.String,System.Boolean,System.Boolean,System.Boolean)");
                    _SharedRelinkMap["Mono.Cecil.ModuleDefinition MonoMod.Utils.Extensions::ReadModule(System.String,Mono.Cecil.ReaderParameters)"] =
                        new RelinkMapEntry("Mono.Cecil.ModuleDefinition", "Mono.Cecil.ModuleDefinition ReadModule(System.String,Mono.Cecil.ReaderParameters)");

                    return _SharedRelinkMap;
                }
            }
            private static Dictionary<string, object> _SharedRelinkMap;

            /// <summary>
            /// Relink a .dll to point towards Celeste.exe and FNA / XNA properly at runtime, then load it.
            /// </summary>
            /// <param name="meta">The mod metadata, used for caching, among other things.</param>
            /// <param name="asmname"></param>
            /// <param name="path">The path of the assembly inside of the mod</param>
            /// <param name="symPath">The path of the assembly's symbols inside of mod, or null</param>
            /// <param name="streamOpener">A callback opening Streams for the assembly and (optionally) its symbols</param>
            /// <returns>The loaded, relinked assembly.</returns>
            internal static Assembly GetRelinkedAssembly(EverestModuleMetadata meta, string asmname, string path, string symPath, Func<(Stream stream, Stream symStream)> streamOpener) {
                lock (RelinkerLock) {
                    // Determine cache paths
                    string cachePath = GetCachedPath(meta, asmname);
                    string cacheChecksumPath = Path.ChangeExtension(cachePath, ".sum");

                    Assembly asm = null;

                    // Try to load the assembly from the cache
                    if (TryLoadCachedAssembly(meta, asmname, path, symPath, cachePath, cacheChecksumPath, out string[] checksums) is not Assembly cacheAsm) {
                        // Delete cached files
                        File.Delete(cachePath);
                        File.Delete(cacheChecksumPath);

                        // Open the assembly streams
                        (Stream stream, Stream symStream) = streamOpener();
                        using (stream)
                        using (symStream) {
                            try {
                                // Relink the assembly
                                if (RelinkAssembly(meta, asmname, stream, symStream, cachePath, out string tmpOutPath) is not Assembly relinkedAsm)
                                    return null;
                                else
                                    asm = relinkedAsm;

                                // Write the checksums for the cached assembly to be loaded in the future
                                // Skip this step if the relinker had to fall back to using a temporary output file
                                if (tmpOutPath == null)
                                    File.WriteAllLines(cacheChecksumPath, checksums);
                            } catch (Exception e) {
                                Logger.Warn("relinker", $"Failed relinking {meta} - {asmname}");
                                Logger.LogDetailed(e);
                                return null;
                            }
                        }
                    } else
                        asm = cacheAsm;

                    Logger.Verbose("relinker", $"Loading assembly for {meta} - {asmname} - {asm.FullName}");
                    return asm;
                }
            }

            private static Assembly TryLoadCachedAssembly(EverestModuleMetadata meta, string asmName, string inPath, string inSymPath, string cachePath, string cacheChecksumsPath, out string[] curChecksums) {
                // Calculate checksums
                // If the stream originates from a 
                List<string> checksums = new List<string>();
                checksums.Add(GameChecksum);

                meta.AssemblyContext.CalcAssemblyCacheChecksums(checksums, inPath, inSymPath);

                curChecksums = checksums.ToArray();

                // Check if the cached assembly + its checksums exist on disk, and if the checksums match
                if (!File.Exists(cachePath) || !File.Exists(cacheChecksumsPath))
                    return null;

                if (!ChecksumsEqual(curChecksums, File.ReadAllLines(cacheChecksumsPath)))
                    return null;
                
                Logger.Verbose("relinker", $"Loading cached assembly for {meta} - {asmName}");

                // Try to load the assembly and the module definition
                try {
                    return meta.AssemblyContext.LoadRelinkedAssembly(cachePath);
                } catch (Exception e) {
                    Logger.Warn("relinker", $"Failed loading cached assembly for {meta} - {asmName}");
                    Logger.LogDetailed(e);
                    return null;
                }
            }

            private static Assembly RelinkAssembly(EverestModuleMetadata meta, string asmname, Stream stream, Stream symStream, string outPath, out string tmpOutPath) {
                tmpOutPath = null;

                // Streams must be seekable
                EnsureStreamIsSeekable(ref stream);
                EnsureStreamIsSeekable(ref symStream);

                // Setup the MonoModder
                // Don't dispose it, as it shares a ton of resources
                MonoModder modder = new LoggedMonoModder() {
                    CleanupEnabled = false,
                    Input = stream,
                    OutputPath = outPath,

                    RelinkModuleMap = new Dictionary<string, ModuleDefinition>(SharedRelinkModuleMap),
                    RelinkMap = new Dictionary<string, object>(SharedRelinkMap),

                    AssemblyResolver = meta.AssemblyContext,
                    MissingDependencyThrow = false
                };
                try {
                    InitMMFlags(modder);

                    // Read and setup debug symbols (if they exist)
                    modder.ReaderParameters.ReadSymbols = symStream != null;
                    modder.ReaderParameters.SymbolStream = symStream;
                    modder.Read();

                    // Check if the assembly name is on the blacklist
                    if (EverestModuleAssemblyContext.AssemblyLoadBlackList.Contains(modder.Module.Assembly.Name.Name, StringComparer.OrdinalIgnoreCase)) {
                        Logger.Warn("relinker", $"Attempted load of blacklisted assembly {meta} - {modder.Module.Assembly.Name}");
                        return null;
                    }

                    // Ensure the runtime rules module is loaded
                    ModuleDefinition runtimeRulesMod = LoadRuntimeRulesModule();

                    // Map assembly dependencies
                    modder.MapDependencies();
                    modder.MapDependencies(runtimeRulesMod);

                    // Patch the assembly
                    TypeDefinition runtimeRulesType = runtimeRulesMod.GetType("MonoMod.MonoModRules");
                    modder.ParseRules(runtimeRulesMod);
                    if (runtimeRulesType != null)
                        runtimeRulesMod.Types.Add(runtimeRulesType); // MonoMod removes the rules type from the assembly

                    modder.ParseRules(modder.Module);

                    modder.AutoPatch();

                    if (!meta.IsNetCoreOnlyMod)
                        NETCoreifier.Coreifier.ConvertToNetCore(modder, sharedDeps: true, preventInlining: true);

                    // Write patched assembly and debug symbols back to disk (always as portable PDBs though)
                    // Fall back to a temporary output path if the given one is unavailable for some reason
                    bool temporaryASM = false;
                    RetryWrite:
                    try {
                        // Try to write with symbols
                        modder.WriterParameters.WriteSymbols = true;
                        modder.WriterParameters.SymbolWriterProvider = new PortablePdbWriterProvider();
                        modder.Write();
                    } catch {
                        try {
                            // Try to write without symbols
                            modder.WriterParameters.SymbolWriterProvider = null;
                            modder.WriterParameters.WriteSymbols = false;
                            modder.Write();
                        } catch (Exception e) when (!temporaryASM) {
                            Logger.Warn("relinker", "Couldn't write to intended output path - falling back to temporary file...");
                            Logger.LogDetailed(e);

                            // Try writing to a temporary file
                            temporaryASM = true;

                            long stamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                            tmpOutPath = Path.Combine(Path.GetTempPath(), $"Everest.Relinked.{Path.GetFileNameWithoutExtension(outPath)}.{stamp}.dll");

                            modder.Module.Name += "." + stamp;
                            modder.Module.Assembly.Name.Name += "." + stamp;
                            modder.OutputPath = tmpOutPath;
                            modder.WriterParameters.WriteSymbols = true;

                            goto RetryWrite;
                        }
                    }
                } finally {
                    modder.Module?.Dispose();
                }

                // Try to load the assembly and the module definition
                try {
                    return meta.AssemblyContext.LoadRelinkedAssembly(outPath);
                } catch (Exception e) {
                    Logger.Warn("relinker", $"Failed loading relinked assembly {meta} - {asmname}");
                    Logger.LogDetailed(e);
                    return null;
                }
            }

            // FIXME: Celeste.Mod.mm.dll caching is currently absolutely borked because GetNextCustomAttribute nukes attributes while iterating :)))
            // Once this is fixed on MM's side, uncomment the caching code again to reduce loading times

            // private static ModuleDefinition _RuntimeRulesModule;
            private static ModuleDefinition LoadRuntimeRulesModule() {
                // if (_RuntimeRulesModule != null)
                //     return _RuntimeRulesModule;

                // Find our rules .Mod.mm.dll
                string rulesPath = Path.Combine(
                    Path.GetDirectoryName(typeof(Celeste).Assembly.Location),
                    Path.GetFileNameWithoutExtension(typeof(Celeste).Assembly.Location) + ".Mod.mm.dll"
                );

                if (!File.Exists(rulesPath)) {
                    // Fallback if someone renamed Celeste.exe
                    rulesPath = Path.Combine(
                        Path.GetDirectoryName(typeof(Celeste).Assembly.Location),
                        "Celeste.Mod.mm.dll"
                    );
                }

                if (!File.Exists(rulesPath))
                    throw new InvalidOperationException($"Couldn't find runtime rules .Mod.mm.dll!");

                // Load the module
                return /* _RuntimeRulesModule = */ ModuleDefinition.ReadModule(rulesPath, new ReaderParameters(ReadingMode.Immediate));
            }

            /// <summary>
            /// Get the cached path of a given mod's relinked .dll
            /// </summary>
            /// <param name="meta">The mod metadata.</param>
            /// <param name="asmname"></param>
            /// <returns>The full path to the cached relinked .dll</returns>
            public static string GetCachedPath(EverestModuleMetadata meta, string asmname)
                => Path.Combine(Loader.PathCache, meta.Name + "." + asmname + ".dll");

            /// <summary>
            /// Determine if both checksum collections are equal.
            /// </summary>
            /// <param name="a">The first checksum array.</param>
            /// <param name="b">The second checksum array.</param>
            /// <returns>True if the contents of both arrays match, false otherwise.</returns>
            public static bool ChecksumsEqual(string[] a, string[] b) {
                if (a.Length != b.Length)
                    return false;
                for (int i = 0; i < a.Length; i++)
                    if (a[i].Trim() != b[i].Trim())
                        return false;
                return true;
            }

            private static void EnsureStreamIsSeekable(ref Stream stream) {
                if (stream == null || stream.CanSeek)
                    return;

                MemoryStream memStream = new MemoryStream();
                stream.CopyTo(memStream);
                stream = memStream;
            }

            [PatchInitMMFlags]
            private static void InitMMFlags(MonoModder modder) {
                // This method is automatically filled via MonoModRules to set the same flags used by Everest itself
            }
            private static void SetMMFlag(MonoModder modder, string key, bool value) => modder.SharedData[key] = value;

        }
    }
}
