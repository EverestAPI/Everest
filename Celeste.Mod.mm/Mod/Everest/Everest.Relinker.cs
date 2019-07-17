using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using MonoMod;
using MonoMod.Utils;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Ionic.Zip;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Celeste.Mod.Helpers;

namespace Celeste.Mod {
    public static partial class Everest {
        /// <summary>
        /// Relink mods to point towards Celeste.exe and FNA / XNA properly at runtime.
        /// </summary>
        public static class Relinker {

            /// <summary>
            /// The hasher used by Relinker.
            /// </summary>
            public readonly static HashAlgorithm ChecksumHasher = MD5.Create();

            /// <summary>
            /// The current Celeste.exe's checksum.
            /// </summary>
            public static string GameChecksum { get; internal set; }

            internal readonly static Dictionary<string, ModuleDefinition> StaticRelinkModuleCache = new Dictionary<string, ModuleDefinition>() {
                { "MonoMod", ModuleDefinition.ReadModule(typeof(MonoModder).Assembly.Location, new ReaderParameters(ReadingMode.Immediate)) },
                { "Celeste", ModuleDefinition.ReadModule(typeof(Celeste).Assembly.Location, new ReaderParameters(ReadingMode.Immediate)) }
            };
            internal static bool RuntimeRulesParsed = false;

            private static Dictionary<string, ModuleDefinition> _SharedRelinkModuleMap;
            public static Dictionary<string, ModuleDefinition> SharedRelinkModuleMap {
                get {
                    if (_SharedRelinkModuleMap != null)
                        return _SharedRelinkModuleMap;

                    _SharedRelinkModuleMap = new Dictionary<string, ModuleDefinition>();
                    string[] entries = Directory.GetFiles(PathGame);
                    for (int i = 0; i < entries.Length; i++) {
                        string path = entries[i];
                        string name = Path.GetFileName(path);
                        string nameNeutral = name.Substring(0, Math.Max(0, name.Length - 4));
                        if (name.EndsWith(".mm.dll")) {
                            if (name.StartsWith("Celeste."))
                                _SharedRelinkModuleMap[nameNeutral] = StaticRelinkModuleCache["Celeste"];
                            else {
                                Logger.Log(LogLevel.Warn, "relinker", $"Found unknown {name}");
                                int dot = name.IndexOf('.');
                                if (dot < 0)
                                    continue;
                                string nameRelinkedNeutral = name.Substring(0, dot);
                                string nameRelinked = nameRelinkedNeutral + ".dll";
                                string pathRelinked = Path.Combine(Path.GetDirectoryName(path), nameRelinked);
                                if (!File.Exists(pathRelinked))
                                    continue;
                                ModuleDefinition relinked;
                                if (!StaticRelinkModuleCache.TryGetValue(nameRelinkedNeutral, out relinked)) {
                                    relinked = ModuleDefinition.ReadModule(pathRelinked, new ReaderParameters(ReadingMode.Immediate));
                                    StaticRelinkModuleCache[nameRelinkedNeutral] = relinked;
                                }
                                Logger.Log(LogLevel.Verbose, "relinker", $"Remapped to {nameRelinked}");
                                _SharedRelinkModuleMap[nameNeutral] = relinked;
                            }
                        }
                    }
                    return _SharedRelinkModuleMap;
                }
            }

            private static Dictionary<string, object> _SharedRelinkMap;
            public static Dictionary<string, object> SharedRelinkMap {
                get {
                    if (_SharedRelinkMap != null)
                        return _SharedRelinkMap;

                    _SharedRelinkMap = new Dictionary<string, object>();

                    // Find our current XNA flavour and relink all types to it.
                    // This relinks mods from XNA to FNA and from FNA to XNA.

                    AssemblyName[] asmRefs = typeof(Celeste).Assembly.GetReferencedAssemblies();
                    for (int ari = 0; ari < asmRefs.Length; ari++) {
                        AssemblyName asmRef = asmRefs[ari];
                        // Ugly hardcoded supported framework list.
                        if (!asmRef.FullName.ToLowerInvariant().Contains("xna") &&
                            !asmRef.FullName.ToLowerInvariant().Contains("fna") &&
                            !asmRef.FullName.ToLowerInvariant().Contains("monogame") // Contains many differences - we should print a warning.
                        )
                            continue;
                        Assembly asm = Assembly.Load(asmRef);
                        ModuleDefinition module = ModuleDefinition.ReadModule(asm.Location, new ReaderParameters(ReadingMode.Immediate));
                        SharedRelinkModuleMap[asmRef.FullName] = SharedRelinkModuleMap[asmRef.Name] = module;
                        Type[] types = asm.GetExportedTypes();
                        for (int i = 0; i < types.Length; i++) {
                            Type type = types[i];
                            TypeDefinition typeDef = module.GetType(type.FullName) ?? module.GetType(type.FullName.Replace('+', '/'));
                            if (typeDef == null)
                                continue;
                            SharedRelinkMap[typeDef.FullName] = typeDef;
                        }
                    }

                    return _SharedRelinkMap;
                }
            }

            private static MonoModder _Modder;
            public static MonoModder Modder {
                get {
                    if (_Modder != null)
                        return _Modder;

                    _Modder = new MonoModder() {
                        CleanupEnabled = false,
                        RelinkModuleMap = SharedRelinkModuleMap,
                        RelinkMap = SharedRelinkMap,
                        DependencyDirs = {
                            PathGame
                        },
                        ReaderParameters = {
                            SymbolReaderProvider = new RelinkerSymbolReaderProvider()
                        }
                    };

                    return _Modder;
                }
                set {
                    _Modder = value;
                }
            }

            /// <summary>
            /// Relink a .dll to point towards Celeste.exe and FNA / XNA properly at runtime, then load it.
            /// </summary>
            /// <param name="meta">The mod metadata, used for caching, among other things.</param>
            /// <param name="stream">The stream to read the .dll from.</param>
            /// <param name="depResolver">An optional dependency resolver.</param>
            /// <param name="checksumsExtra">Any optional checksums. If you're running this at runtime, pass at least Everest.Relinker.GetChecksum(Metadata)</param>
            /// <param name="prePatch">An optional step executed before patching, but after MonoMod has loaded the input assembly.</param>
            /// <returns>The loaded, relinked assembly.</returns>
            public static Assembly GetRelinkedAssembly(EverestModuleMetadata meta, Stream stream,
                MissingDependencyResolver depResolver = null, string[] checksumsExtra = null, Action<MonoModder> prePatch = null) {
                if (!Flags.SupportRelinkingMods) {
                    Logger.Log(LogLevel.Warn, "relinker", "Relinker disabled!");
                    return null;
                }

                string cachedPath = GetCachedPath(meta);
                string cachedChecksumPath = cachedPath.Substring(0, cachedPath.Length - 4) + ".sum";

                string[] checksums = new string[2 + (checksumsExtra?.Length ?? 0)];
                if (GameChecksum == null)
                    GameChecksum = Everest.GetChecksum(Assembly.GetAssembly(typeof(Relinker)).Location).ToHexadecimalString();
                checksums[0] = GameChecksum;

                checksums[1] = Everest.GetChecksum(meta).ToHexadecimalString();

                if (checksumsExtra != null)
                    for (int i = 0; i < checksumsExtra.Length; i++) {
                        checksums[i + 2] = checksumsExtra[i];
                    }

                if (File.Exists(cachedPath) && File.Exists(cachedChecksumPath) &&
                    ChecksumsEqual(checksums, File.ReadAllLines(cachedChecksumPath))) {
                    Logger.Log(LogLevel.Verbose, "relinker", $"Loading cached assembly for {meta}");
                    try {
                        return Assembly.LoadFrom(cachedPath);
                    } catch (Exception e) {
                        Logger.Log(LogLevel.Warn, "relinker", $"Failed loading {meta}");
                        e.LogDetailed();
                        return null;
                    }
                }

                if (depResolver == null)
                    depResolver = GenerateModDependencyResolver(meta);

                try {
                    MonoModder modder = Modder;

                    modder.Input = stream;
                    modder.OutputPath = cachedPath;
                    modder.MissingDependencyResolver = depResolver;

                    string symbolPath;
                    modder.ReaderParameters.SymbolStream = OpenStream(meta, out symbolPath, meta.DLL.Substring(0, meta.DLL.Length - 4) + ".pdb", meta.DLL + ".mdb");
                    modder.ReaderParameters.ReadSymbols = modder.ReaderParameters.SymbolStream != null;
                    if (modder.ReaderParameters.SymbolReaderProvider != null &&
                        modder.ReaderParameters.SymbolReaderProvider is RelinkerSymbolReaderProvider) {
                        ((RelinkerSymbolReaderProvider) modder.ReaderParameters.SymbolReaderProvider).Format =
                            string.IsNullOrEmpty(symbolPath) ? DebugSymbolFormat.Auto :
                            symbolPath.EndsWith(".mdb") ? DebugSymbolFormat.MDB :
                            symbolPath.EndsWith(".pdb") ? DebugSymbolFormat.PDB :
                            DebugSymbolFormat.Auto;
                    }

                    modder.Read();

                    modder.ReaderParameters.ReadSymbols = false;

                    if (modder.ReaderParameters.SymbolReaderProvider != null &&
                        modder.ReaderParameters.SymbolReaderProvider is RelinkerSymbolReaderProvider) {
                        ((RelinkerSymbolReaderProvider) modder.ReaderParameters.SymbolReaderProvider).Format = DebugSymbolFormat.Auto;
                    }

                    modder.MapDependencies();

                    if (!RuntimeRulesParsed) {
                        RuntimeRulesParsed = true;

                        InitMMSharedData();

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
                        if (File.Exists(rulesPath)) {
                            ModuleDefinition rules = ModuleDefinition.ReadModule(rulesPath, new ReaderParameters(ReadingMode.Immediate));
                            modder.ParseRules(rules);
                            rules.Dispose(); // Is this safe?
                        }

                        // Fix old mods built against HookIL instead of ILContext.
                        _Modder.RelinkMap["MonoMod.RuntimeDetour.HookGen.ILManipulator"] = "MonoMod.Cil.ILContext/Manipulator";
                        _Modder.RelinkMap["MonoMod.RuntimeDetour.HookGen.HookIL"] = "MonoMod.Cil.ILContext";
                        _Modder.RelinkMap["MonoMod.RuntimeDetour.HookGen.HookILCursor"] = "MonoMod.Cil.ILCursor";
                        _Modder.RelinkMap["MonoMod.RuntimeDetour.HookGen.HookILLabel"] = "MonoMod.Cil.ILLabel";
                        _Modder.RelinkMap["MonoMod.RuntimeDetour.HookGen.HookExtensions"] = "MonoMod.Cil.ILPatternMatchingExt";

                        _Shim("MonoMod.Utils.ReflectionHelper", typeof(MonoModUpdateShim._ReflectionHelper));
                        _Shim("MonoMod.Cil.ILCursor", typeof(MonoModUpdateShim._ILCursor));

                        // If no entry for MonoMod.Utils exists already, add one.
                        modder.MapDependency(_Modder.Module, "MonoMod.Utils");
                    }

                    prePatch?.Invoke(modder);

                    modder.AutoPatch();

                    modder.Write();
                } catch (Exception e) {
                    Logger.Log(LogLevel.Warn, "relinker", $"Failed relinking {meta}");
                    e.LogDetailed();
                    return null;
                } finally {
                    Modder.ClearCaches(moduleSpecific: true);
                    Modder.Module.Dispose();
                    Modder.Module = null;
                    Modder.ReaderParameters.SymbolStream?.Dispose();
                }

                if (File.Exists(cachedChecksumPath)) {
                    File.Delete(cachedChecksumPath);
                }
                File.WriteAllLines(cachedChecksumPath, checksums);

                Logger.Log(LogLevel.Verbose, "relinker", $"Loading assembly for {meta}");
                try {
                    return Assembly.LoadFrom(cachedPath);
                } catch (Exception e) {
                    Logger.Log(LogLevel.Warn, "relinker", $"Failed loading {meta}");
                    e.LogDetailed();
                    return null;
                }
            }


            private static void _Shim(string fromType, Type toType) {
                string toTypeName = toType.FullName.Replace("+", "/");
                foreach (FieldInfo to in toType.GetFields(BindingFlags.Public | BindingFlags.Static)) {
                    _Modder.RelinkMap[to.GetCustomAttribute<ShimFromAttribute>()?.FindableID ?? $"{to.FieldType.FullName.Replace("+", "/")} {fromType}::{to.Name}"] = new RelinkMapEntry(toTypeName, to.Name);
                }
                foreach (MethodInfo to in toType.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                    _Modder.RelinkMap[to.GetCustomAttribute<ShimFromAttribute>()?.FindableID ?? to.GetFindableID(type: fromType, proxyMethod: true, simple: true)] = new RelinkMapEntry(toTypeName, to.GetFindableID(withType: false, simple: true));
                }
            }


            private static MissingDependencyResolver GenerateModDependencyResolver(EverestModuleMetadata meta) {
                if (!string.IsNullOrEmpty(meta.PathArchive)) {
                    return delegate (MonoModder mod, ModuleDefinition main, string name, string fullName) {
                        string asmName = name + ".dll";
                        using (ZipFile zip = new ZipFile(meta.PathArchive)) {
                            foreach (ZipEntry entry in zip.Entries) {
                                if (entry.FileName != asmName)
                                    continue;
                                using (MemoryStream stream = entry.ExtractStream()) {
                                    return ModuleDefinition.ReadModule(stream, mod.GenReaderParameters(false));
                                }
                            }
                        }
                        return null;
                    };
                }

                if (!string.IsNullOrEmpty(meta.PathDirectory)) {
                    return delegate (MonoModder mod, ModuleDefinition main, string name, string fullName) {
                        string asmPath = Path.Combine(meta.PathDirectory, name + ".dll");
                        if (!File.Exists(asmPath))
                            return null;
                        return ModuleDefinition.ReadModule(asmPath, mod.GenReaderParameters(false, asmPath));
                    };
                }

                return null;
            }

            private static Stream OpenStream(EverestModuleMetadata meta, out string result, params string[] names) {
                if (!string.IsNullOrEmpty(meta.PathArchive)) {
                    using (ZipFile zip = new ZipFile(meta.PathArchive)) {
                        foreach (ZipEntry entry in zip.Entries) {
                            if (!names.Contains(entry.FileName))
                                continue;
                            result = entry.FileName;
                            return entry.ExtractStream();
                        }
                    }
                    result = null;
                    return null;
                }

                if (!string.IsNullOrEmpty(meta.PathDirectory)) {
                    foreach (string name in names) {
                        string path = name;
                        if (!File.Exists(path))
                            path = Path.Combine(meta.PathDirectory, name);
                        if (!File.Exists(path))
                            continue;
                        result = path;
                        return File.OpenRead(path);
                    }
                }

                result = null;
                return null;
            }

            /// <summary>
            /// Get the cached path of a given mod's relinked .dll
            /// </summary>
            /// <param name="meta">The mod metadata.</param>
            /// <returns>The full path to the cached relinked .dll</returns>
            public static string GetCachedPath(EverestModuleMetadata meta)
                => Path.Combine(Loader.PathCache, meta.Name + "." + Path.GetFileNameWithoutExtension(meta.DLL) + ".dll");

            /// <summary>
            /// Get the checksum for a given mod's .dll or the containing .zip
            /// </summary>
            /// <param name="meta">The mod metadata.</param>
            /// <returns>A checksum.</returns>
            [Obsolete("Use meta.Hash instead.")]
            public static string GetChecksum(EverestModuleMetadata meta) {
                string path = meta.PathArchive;
                if (string.IsNullOrEmpty(path))
                    path = meta.DLL;
                return GetChecksum(path);
            }
            /// <summary>
            /// Get the checksum for a given file.
            /// </summary>
            /// <param name="path">The file path.</param>
            /// <returns>A checksum.</returns>
            [Obsolete("Use Everest.GetChecksum instead.")]
            public static string GetChecksum(string path) {
                using (FileStream fs = File.OpenRead(path))
                    return ChecksumHasher.ComputeHash(fs).ToHexadecimalString();
            }

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

            [PatchInitMMSharedData]
            private static void InitMMSharedData() {
                // This method is automatically filled via MonoModRules.
            }
            private static void SetMMSharedData(string key, bool value) {
                MonoModExt.SharedData[key] = value;
            }

        }
    }
}
