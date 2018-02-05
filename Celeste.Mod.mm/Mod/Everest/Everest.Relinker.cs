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
using System.IO.Compression;
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
                        if (!asmRef.FullName.Contains("XNA") &&
                            !asmRef.FullName.Contains("FNA") &&
                            !asmRef.FullName.Contains("MonoGame") // Contains many differences - we should print a warning.
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

            public static Assembly GetRelinkedAssembly(EverestModuleMetadata meta, Stream stream, MissingDependencyResolver depResolver = null) {
                string name = Path.GetFileName(meta.DLL);
                string cachedName = meta.Name + "." + name.Substring(0, name.Length - 3) + "dll";
                string cachedPath = Path.Combine(Loader.PathCache, cachedName);
                string cachedChecksumPath = Path.Combine(Loader.PathCache, cachedName + ".sum");

                string[] checksums = new string[2];
                using (MD5 md5 = MD5.Create()) {
                    if (GameChecksum == null)
                        using (FileStream fs = File.OpenRead(Assembly.GetAssembly(typeof(Relinker)).Location))
                            GameChecksum = md5.ComputeHash(fs).ToHexadecimalString();
                    checksums[0] = GameChecksum;

                    string modPath = meta.PathArchive;
                    if (modPath.Length == 0)
                        modPath = meta.DLL;
                    using (FileStream fs = File.OpenRead(modPath))
                        checksums[1] = md5.ComputeHash(fs).ToHexadecimalString();
                }

                if (File.Exists(cachedPath) && File.Exists(cachedChecksumPath) &&
                    ChecksumsEqual(checksums, File.ReadAllLines(cachedChecksumPath)))
                    return Assembly.LoadFrom(cachedPath);

                if (depResolver == null)
                    depResolver = GenerateModDependencyResolver(meta);

                using (MonoModder modder = new MonoModder() {
                    Input = stream,
                    OutputPath = cachedPath,
                    CleanupEnabled = false,
                    RelinkModuleMap = SharedRelinkModuleMap,
                    RelinkMap = SharedRelinkMap,
                    DependencyDirs = {
                        PathGame
                    },
                    MissingDependencyResolver = depResolver
                })
                    try {
                        modder.ReaderParameters.ReadSymbols = false;
                        modder.WriterParameters.WriteSymbols = false;
                        modder.WriterParameters.SymbolWriterProvider = null;

                        modder.Read();
                        modder.MapDependencies();
                        modder.AutoPatch();
                        modder.Write();
                    } catch (Exception e) {
                        Logger.Log("relinker", $"Failed relinking {meta}: {e}");
                        return null;
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
                        using (Stream zipStream = File.OpenRead(meta.PathArchive))
                        using (ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Read)) {
                            foreach (ZipArchiveEntry entry in zip.Entries) {
                                if (entry.FullName != asmName)
                                    continue;
                                using (Stream stream = entry.Open())
                                using (MemoryStream ms = new MemoryStream()) {
                                    stream.CopyTo(ms);
                                    ms.Seek(0, SeekOrigin.Begin);
                                    return ModuleDefinition.ReadModule(ms, mod.GenReaderParameters(false));
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
