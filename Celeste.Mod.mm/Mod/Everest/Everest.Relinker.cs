using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using MonoMod;
using MonoMod.Helpers;
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

namespace Celeste.Mod {
    public static partial class Everest {
        /// <summary>
        /// Relink mods to point towards Celeste.exe and FNA.dll properly at runtime.
        /// </summary>
        public static class Relinker {

            public static HashAlgorithm ChecksumHasher = MD5.Create();

            public static string GameChecksum;

            public readonly static IDictionary<string, ModuleDefinition> StaticRelinkModuleCache = new FastDictionary<string, ModuleDefinition>() {
                { "MonoMod", ModuleDefinition.ReadModule(typeof(MonoModder).Assembly.Location, new ReaderParameters(ReadingMode.Immediate)) },
                { "Celeste", ModuleDefinition.ReadModule(typeof(Celeste).Assembly.Location, new ReaderParameters(ReadingMode.Immediate)) },
            };

            private static FastDictionary<string, ModuleDefinition> _SharedRelinkModuleMap;
            public static IDictionary<string, ModuleDefinition> SharedRelinkModuleMap {
                get {
                    if (_SharedRelinkModuleMap != null)
                        return _SharedRelinkModuleMap;

                    _SharedRelinkModuleMap = new FastDictionary<string, ModuleDefinition>();
                    string[] entries = Directory.GetFiles(PathGame);
                    for (int i = 0; i < entries.Length; i++) {
                        string path = entries[i];
                        string name = Path.GetFileName(path);
                        string nameNeutral = name.Substring(0, Math.Max(0, name.Length - 4));
                        if (name.EndsWith(".mm.dll")) {
                            if (name.StartsWith("Celeste."))
                                _SharedRelinkModuleMap[nameNeutral] = StaticRelinkModuleCache["Celeste"];
                            else {
                                Logger.Log("relinker", $"Found unknown {name}");
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
                                Logger.Log("relinker", $"Remapped to {nameRelinked}");
                                _SharedRelinkModuleMap[nameNeutral] = relinked;
                            }
                        }
                    }
                    return _SharedRelinkModuleMap;
                }
            }

            private static FastDictionary<string, object> _SharedRelinkMap;
            public static IDictionary<string, object> SharedRelinkMap {
                get {
                    if (_SharedRelinkMap != null)
                        return _SharedRelinkMap;

                    _SharedRelinkMap = new FastDictionary<string, object>();

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
                        }
                    };

                    _Modder.ReaderParameters.ReadSymbols = false;
                    _Modder.WriterParameters.WriteSymbols = false;
                    _Modder.WriterParameters.SymbolWriterProvider = null;

                    _Modder.Relinker = _Modder.DefaultUncachedRelinker;

                    return _Modder;
                }
                set {
                    _Modder = value;
                }
            }

            public static Assembly GetRelinkedAssembly(EverestModuleMetadata meta, Stream stream,
                MissingDependencyResolver depResolver = null, string[] checksumsExtra = null, Action<MonoModder> prePatch = null) {
                string cachedPath = GetCachedPath(meta);
                string cachedChecksumPath = cachedPath.Substring(0, cachedPath.Length - 4) + ".sum";

                string[] checksums = new string[2 + (checksumsExtra?.Length ?? 0)];
                if (GameChecksum == null)
                    GameChecksum = GetChecksum(Assembly.GetAssembly(typeof(Relinker)).Location);
                checksums[0] = GameChecksum;

                checksums[1] = GetChecksum(meta);

                if (checksumsExtra != null)
                    for (int i = 0; i < checksumsExtra.Length; i++) {
                        checksums[i + 2] = checksumsExtra[i];
                    }

                if (File.Exists(cachedPath) && File.Exists(cachedChecksumPath) &&
                    ChecksumsEqual(checksums, File.ReadAllLines(cachedChecksumPath)))
                    return Assembly.LoadFrom(cachedPath);

                if (depResolver == null)
                    depResolver = GenerateModDependencyResolver(meta);

                try {
                    MonoModder modder = Modder;

                    modder.Input = stream;
                    modder.OutputPath = cachedPath;
                    modder.MissingDependencyResolver = depResolver;

                    modder.Read();
                    modder.MapDependencies();
                    prePatch?.Invoke(modder);
                    modder.AutoPatch();
                    modder.Write();
                } catch (Exception e) {
                    Logger.Log("relinker", $"Failed relinking {meta}: {e}");
                    return null;
                } finally {
                    Modder.ClearCaches(moduleSpecific: true);
                    Modder.Module.Dispose();
                    Modder.Module = null;
                }

                if (File.Exists(cachedChecksumPath)) {
                    File.Delete(cachedChecksumPath);
                }
                File.WriteAllLines(cachedChecksumPath, checksums);

                return Assembly.LoadFrom(cachedPath);
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

            public static string GetCachedPath(EverestModuleMetadata meta)
                => Path.Combine(Loader.PathCache, meta.Name + "." + Path.GetFileNameWithoutExtension(meta.DLL) + ".dll");

            public static string GetChecksum(EverestModuleMetadata meta) {
                string path = meta.PathArchive;
                if (string.IsNullOrEmpty(path))
                    path = meta.DLL;
                return GetChecksum(path);
            }
            public static string GetChecksum(string path) {
                using (FileStream fs = File.OpenRead(path))
                    return ChecksumHasher.ComputeHash(fs).ToHexadecimalString();
            }

            public static bool ChecksumsEqual(string[] a, string[] b) {
                if (a.Length != b.Length)
                    return false;
                for (int i = 0; i < a.Length; i++)
                    if (a[i].Trim() != b[i].Trim())
                        return false;
                return true;
            }

        }
    }
}
