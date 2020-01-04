using Microsoft.Xna.Framework.Graphics;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Ionic.Zip;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MonoMod.Utils;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MCC = Mono.Cecil.Cil;
using MonoMod.Cil;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod {
    public static partial class Everest {
        public static class Loader {

            /// <summary>
            /// The path to the Everest /Mods directory.
            /// </summary>
            public static string PathMods { get; internal set; }
            /// <summary>
            /// The path to the Everest /Mods/Cache directory.
            /// </summary>
            public static string PathCache { get; internal set; }

            /// <summary>
            /// The path to the Everest /Mods/blacklist.txt file.
            /// </summary>
            public static string PathBlacklist { get; internal set; }
            internal static List<string> _Blacklist = new List<string>();
            /// <summary>
            /// The currently loaded mod blacklist.
            /// </summary>
            public static ReadOnlyCollection<string> Blacklist => _Blacklist?.AsReadOnly();

            /// <summary>
            /// The path to the Everest /Mods/whitelist.txt file.
            /// </summary>
            public static string PathWhitelist { get; internal set; }
            internal static string NameWhitelist;
            internal static List<string> _Whitelist;
            /// <summary>
            /// The currently loaded mod whitelist.
            /// </summary>
            public static ReadOnlyCollection<string> Whitelist => _Whitelist?.AsReadOnly();

            internal static List<Tuple<EverestModuleMetadata, Action>> Delayed = new List<Tuple<EverestModuleMetadata, Action>>();
            internal static int DelayedLock;

            /// <summary>
            /// All mods on this list with a version lower than the specified version will never load.
            /// </summary>
            internal static Dictionary<string, Version> PermanentBlacklist = new Dictionary<string, Version>() {

                // Note: Most, if not all mods use Major.Minor.Build
                // Revision is thus set to -1 and < 0
                // Entries with a revision of 0 are there because there is no update / fix for those mods.

                // Versions of the mods older than on this list no longer work with Celeste 1.3.0.0
                { "SpeedrunTool", new Version(1, 5, 7) },
                { "CrystalValley", new Version(1, 1, 3) },
                { "IsaGrabBag", new Version(1, 3, 2) },
                { "testroom", new Version(1, 0, 1, 0) },
                { "Elemental Chaos", new Version(1, 0, 0, 0) },
                { "BGswitch", new Version(0, 1, 0, 0) },

            };

            internal static void LoadAuto() {
                Directory.CreateDirectory(PathMods = Path.Combine(PathEverest, "Mods"));
                Directory.CreateDirectory(PathCache = Path.Combine(PathMods, "Cache"));

                PathBlacklist = Path.Combine(PathMods, "blacklist.txt");
                if (File.Exists(PathBlacklist)) {
                    _Blacklist = File.ReadAllLines(PathBlacklist).Select(l => (l.StartsWith("#") ? "" : l).Trim()).ToList();
                } else {
                    using (StreamWriter writer = File.CreateText(PathBlacklist)) {
                        writer.WriteLine("# This is the blacklist. Lines starting with # are ignored.");
                        writer.WriteLine("ExampleFolder");
                        writer.WriteLine("SomeMod.zip");
                    }
                }

                if (!string.IsNullOrEmpty(NameWhitelist)) {
                    PathWhitelist = Path.Combine(PathMods, NameWhitelist);
                    if (File.Exists(PathWhitelist)) {
                        _Whitelist = File.ReadAllLines(PathWhitelist).Select(l => (l.StartsWith("#") ? "" : l).Trim()).ToList();
                    }
                }

                if (Flags.IsDisabled)
                    return;

                string[] files = Directory.GetFiles(PathMods);
                for (int i = 0; i < files.Length; i++) {
                    string file = Path.GetFileName(files[i]);
                    if (!file.EndsWith(".zip") || _Blacklist.Contains(file))
                        continue;
                    if (_Whitelist != null && !_Whitelist.Contains(file))
                        continue;
                    LoadZip(file);
                }
                files = Directory.GetDirectories(PathMods);
                for (int i = 0; i < files.Length; i++) {
                    string file = Path.GetFileName(files[i]);
                    if (file == "Cache" || _Blacklist.Contains(file))
                        continue;
                    if (_Whitelist != null && !_Whitelist.Contains(file))
                        continue;
                    LoadDir(file);
                }

            }

            /// <summary>
            /// Load a mod from a .zip archive at runtime.
            /// </summary>
            /// <param name="archive">The path to the mod .zip archive.</param>
            public static void LoadZip(string archive) {
                if (Flags.IsDisabled || !Flags.SupportRuntimeMods) {
                    Logger.Log(LogLevel.Warn, "loader", "Loader disabled!");
                    return;
                }

                if (!File.Exists(archive)) // Relative path? Let's just make it absolute.
                    archive = Path.Combine(PathMods, archive);
                if (!File.Exists(archive)) // It just doesn't exist.
                    return;

                Logger.Log(LogLevel.Verbose, "loader", $"Loading mod .zip: {archive}");

                EverestModuleMetadata meta = null;
                EverestModuleMetadata[] multimetas = null;

                // In case the icon appears before the metadata in the .zip, store it temporarily, set it later.
                Texture2D icon = null;
                using (ZipFile zip = new ZipFile(archive)) {
                    foreach (ZipEntry entry in zip.Entries) {
                        if (entry.FileName == "metadata.yaml") {
                            using (MemoryStream stream = entry.ExtractStream())
                            using (StreamReader reader = new StreamReader(stream)) {
                                try {
                                    meta = YamlHelper.Deserializer.Deserialize<EverestModuleMetadata>(reader);
                                    meta.PathArchive = archive;
                                    meta.PostParse();
                                } catch (Exception e) {
                                    Logger.Log(LogLevel.Warn, "loader", $"Failed parsing metadata.yaml in {archive}: {e}");
                                }
                            }
                            continue;
                        }
                        if (entry.FileName == "multimetadata.yaml" ||
                            entry.FileName == "everest.yaml" ||
                            entry.FileName == "everest.yml") {
                            using (MemoryStream stream = entry.ExtractStream())
                            using (StreamReader reader = new StreamReader(stream)) {
                                try {
                                    if (!reader.EndOfStream) {
                                        multimetas = YamlHelper.Deserializer.Deserialize<EverestModuleMetadata[]>(reader);
                                        foreach (EverestModuleMetadata multimeta in multimetas) {
                                            multimeta.PathArchive = archive;
                                            multimeta.PostParse();
                                        }
                                    }
                                } catch (Exception e) {
                                    Logger.Log(LogLevel.Warn, "loader", $"Failed parsing multimetadata.yaml in {archive}: {e}");
                                }
                            }
                            continue;
                        }
                        if (entry.FileName == "icon.png") {
                            using (Stream stream = entry.ExtractStream())
                                icon = Texture2D.FromStream(Celeste.Instance.GraphicsDevice, stream);
                            continue;
                        }
                    }
                }

                if (meta != null) {
                    if (icon != null)
                        meta.Icon = icon;
                }

                ZipModContent contentMeta = new ZipModContent(archive);
                EverestModuleMetadata contentMetaParent = null;

                Action contentCrawl = () => {
                    if (contentMeta == null)
                        return;
                    if (contentMetaParent != null) {
                        contentMeta.Mod = contentMetaParent;
                        contentMeta.Name = contentMetaParent.Name;
                    }
                    Content.Crawl(contentMeta);
                    contentMeta = null;
                };

                if (multimetas != null) {
                    foreach (EverestModuleMetadata multimeta in multimetas) {
                        multimeta.Multimeta = multimetas;
                        if (contentMetaParent == null)
                            contentMetaParent = multimeta;
                        LoadModDelayed(multimeta, contentCrawl);
                    }
                } else {
                    if (meta == null) {
                        meta = new EverestModuleMetadata() {
                            Name = "_zip_" + Path.GetFileNameWithoutExtension(archive),
                            VersionString = "0.0.0-dummy",
                            PathArchive = archive
                        };
                        meta.PostParse();
                    }
                    contentMetaParent = meta;
                    LoadModDelayed(meta, contentCrawl);
                }
            }

            /// <summary>
            /// Load a mod from a directory at runtime.
            /// </summary>
            /// <param name="dir">The path to the mod directory.</param>
            public static void LoadDir(string dir) {
                if (Flags.IsDisabled || !Flags.SupportRuntimeMods) {
                    Logger.Log(LogLevel.Warn, "loader", "Loader disabled!");
                    return;
                }

                if (!Directory.Exists(dir)) // Relative path?
                    dir = Path.Combine(PathMods, dir);
                if (!Directory.Exists(dir)) // It just doesn't exist.
                    return;

                Logger.Log(LogLevel.Verbose, "loader", $"Loading mod directory: {dir}");

                EverestModuleMetadata meta = null;
                EverestModuleMetadata[] multimetas = null;

                string metaPath = Path.Combine(dir, "metadata.yaml");
                if (File.Exists(metaPath))
                    using (StreamReader reader = new StreamReader(metaPath)) {
                        try {
                            meta = YamlHelper.Deserializer.Deserialize<EverestModuleMetadata>(reader);
                            meta.PathDirectory = dir;
                            meta.PostParse();
                        } catch (Exception e) {
                            Logger.Log(LogLevel.Warn, "loader", $"Failed parsing metadata.yaml in {dir}: {e}");
                        }
                    }

                metaPath = Path.Combine(dir, "multimetadata.yaml");
                if (!File.Exists(metaPath))
                    metaPath = Path.Combine(dir, "everest.yaml");
                if (!File.Exists(metaPath))
                    metaPath = Path.Combine(dir, "everest.yml");
                if (File.Exists(metaPath))
                    using (StreamReader reader = new StreamReader(metaPath)) {
                        try {
                            if (!reader.EndOfStream) {
                                multimetas = YamlHelper.Deserializer.Deserialize<EverestModuleMetadata[]>(reader);
                                foreach (EverestModuleMetadata multimeta in multimetas) {
                                    multimeta.PathDirectory = dir;
                                    multimeta.PostParse();
                                }
                            }
                        } catch (Exception e) {
                            Logger.Log(LogLevel.Warn, "loader", $"Failed parsing everest.yaml in {dir}: {e}");
                        }
                    }

                FileSystemModContent contentMeta = new FileSystemModContent(dir);
                EverestModuleMetadata contentMetaParent = null;

                Action contentCrawl = () => {
                    if (contentMeta == null)
                        return;
                    if (contentMetaParent != null) {
                        contentMeta.Mod = contentMetaParent;
                        contentMeta.Name = contentMetaParent.Name;
                    }
                    Content.Crawl(contentMeta);
                    contentMeta = null;
                };

                if (multimetas != null) {
                    foreach (EverestModuleMetadata multimeta in multimetas) {
                        multimeta.Multimeta = multimetas;
                        if (contentMetaParent == null)
                            contentMetaParent = multimeta;
                        LoadModDelayed(multimeta, contentCrawl);
                    }
                } else {
                    if (meta == null) {
                        meta = new EverestModuleMetadata() {
                            Name = "_dir_" + Path.GetFileName(dir),
                            VersionString = "0.0.0-dummy",
                            PathDirectory = dir
                        };
                        meta.PostParse();
                    }
                    contentMetaParent = meta;
                    LoadModDelayed(meta, contentCrawl);
                }
            }

            /// <summary>
            /// Load a mod .dll given its metadata at runtime. Doesn't load the mod content.
            /// If required, loads the mod after all of its dependencies have been loaded.
            /// </summary>
            /// <param name="meta">Metadata of the mod to load.</param>
            /// <param name="callback">Callback to be executed after the mod has been loaded. Executed immediately if meta == null.</param>
            public static void LoadModDelayed(EverestModuleMetadata meta, Action callback) {
                if (Flags.IsDisabled || !Flags.SupportRuntimeMods) {
                    Logger.Log(LogLevel.Warn, "loader", "Loader disabled!");
                    return;
                }

                if (meta == null) {
                    callback?.Invoke();
                    return;
                }

                if (DependencyLoaded(meta)) {
                    Logger.Log(LogLevel.Warn, "loader", $"Mod {meta} already loaded!");
                    return;
                }

                if (PermanentBlacklist.TryGetValue(meta.Name, out Version minver) && meta.Version < minver) {
                    Logger.Log(LogLevel.Warn, "loader", $"Mod {meta} permanently blacklisted by Everest!");
                    return;
                }


                foreach (EverestModuleMetadata dep in meta.Dependencies)
                    if (!DependencyLoaded(dep)) {
                        Logger.Log(LogLevel.Info, "loader", $"Dependency {dep} of mod {meta} not loaded! Delaying.");
                        lock (Delayed) {
                            Delayed.Add(Tuple.Create(meta, callback));
                        }
                        return;
                    }

                callback?.Invoke();

                LoadMod(meta);
            }

            /// <summary>
            /// Load a mod .dll given its metadata at runtime. Doesn't load the mod content.
            /// </summary>
            /// <param name="meta">Metadata of the mod to load.</param>
            public static void LoadMod(EverestModuleMetadata meta) {
                if (Flags.IsDisabled || !Flags.SupportRuntimeMods) {
                    Logger.Log(LogLevel.Warn, "loader", "Loader disabled!");
                    return;
                }

                if (meta == null)
                    return;

                // Add an AssemblyResolve handler for all bundled libraries.
                AppDomain.CurrentDomain.AssemblyResolve += GenerateModAssemblyResolver(meta);

                ApplyRelinkerHackfixes(meta);

                // Load the actual assembly.
                Assembly asm = null;
                if (!string.IsNullOrEmpty(meta.PathArchive)) {
                    bool returnEarly = false;
                    using (ZipFile zip = new ZipFile(meta.PathArchive)) {
                        foreach (ZipEntry entry in zip.Entries) {
                            string entryName = entry.FileName.Replace('\\', '/');
                            if (entryName == meta.DLL) {
                                using (MemoryStream stream = entry.ExtractStream())
                                    asm = Relinker.GetRelinkedAssembly(meta, stream);
                            }

                            if (entryName == "main.lua") {
                                new LuaModule(meta).Register();
                                returnEarly = true;
                            }
                        }
                    }

                    if (returnEarly)
                        return;

                } else {
                    if (!string.IsNullOrEmpty(meta.DLL) && File.Exists(meta.DLL)) {
                            using (FileStream stream = File.OpenRead(meta.DLL))
                                asm = Relinker.GetRelinkedAssembly(meta, stream);
                    }

                    if (File.Exists(Path.Combine(meta.PathDirectory, "main.lua"))) {
                        new LuaModule(meta).Register();
                        return;
                    }
                }

                ApplyModHackfixes(meta, asm);

                if (asm == null) {
                    // Register a null module for content mods.
                    new NullModule(meta).Register();
                    return;
                }

                LoadModAssembly(meta, asm);
            }

            /// <summary>
            /// Find and load all EverestModules in the given assembly.
            /// </summary>
            /// <param name="meta">The mod metadata, preferably from the mod metadata.yaml file.</param>
            /// <param name="asm">The mod assembly, preferably relinked.</param>
            public static void LoadModAssembly(EverestModuleMetadata meta, Assembly asm) {
                if (Flags.IsDisabled || !Flags.SupportRuntimeMods) {
                    Logger.Log(LogLevel.Warn, "loader", "Loader disabled!");
                    return;
                }

                Content.Crawl(new AssemblyModContent(asm) {
                    Mod = meta,
                    Name = meta.Name
                });

                Type[] types;
                try {
                    types = asm.GetTypes();
                } catch (Exception e) {
                    Logger.Log(LogLevel.Warn, "loader", $"Failed reading assembly: {e}");
                    e.LogDetailed();
                    return;
                }

                for (int i = 0; i < types.Length; i++) {
                    Type type = types[i];

                    if (typeof(EverestModule).IsAssignableFrom(type) && !type.IsAbstract && !typeof(NullModule).IsAssignableFrom(type)) {
                        EverestModule mod = (EverestModule) type.GetConstructor(_EmptyTypeArray).Invoke(_EmptyObjectArray);
                        mod.Metadata = meta;
                        mod.Register();
                    }
                }
            }

            /// <summary>
            /// Checks if all dependencies are loaded.
            /// Can be used by mods manually to f.e. activate / disable functionality.
            /// </summary>
            /// <param name="meta">The metadata of the mod listing the dependencies.</param>
            /// <returns>True if the dependencies have already been loaded by Everest, false otherwise.</returns>
            public static bool DependenciesLoaded(EverestModuleMetadata meta) {
                if (Flags.IsDisabled || !Flags.SupportRuntimeMods) {
                    return false;
                }

                foreach (EverestModuleMetadata dep in meta.Dependencies)
                    if (!DependencyLoaded(dep))
                        return false;
                return true;
            }

            /// <summary>
            /// Checks if an dependency is loaded.
            /// Can be used by mods manually to f.e. activate / disable functionality.
            /// </summary>
            /// <param name="dep">Dependency to check for. Name and Version will be checked.</param>
            /// <returns>True if the dependency has already been loaded by Everest, false otherwise.</returns>
            public static bool DependencyLoaded(EverestModuleMetadata dep) {
                string depName = dep.Name;
                Version depVersion = dep.Version;

                foreach (EverestModule other in _Modules) {
                    EverestModuleMetadata meta = other.Metadata;
                    if (meta.Name != depName)
                        continue;

                    Version version = meta.Version;
                    return VersionSatisfiesDependency(depVersion, version);
                }

                return false;
            }

            /// <summary>
            /// Checks if the given version number is "compatible" with the one required as a dependency.
            /// </summary>
            /// <param name="requiredVersion">The version required by a mod in their dependencies</param>
            /// <param name="installedVersion">The version to check for</param>
            /// <returns>true if the versions number are compatible, false otherwise.</returns>
            public static bool VersionSatisfiesDependency(Version requiredVersion, Version installedVersion) {
                // Special case: Always true if version == 0.0.*
                if (installedVersion.Major == 0 && installedVersion.Minor == 0)
                    return true;

                // Major version, breaking changes, must match.
                if (installedVersion.Major != requiredVersion.Major)
                    return false;
                // Minor version, non-breaking changes, installed can't be lower than what we depend on.
                if (installedVersion.Minor < requiredVersion.Minor)
                    return false;

                // "Build" is "PATCH" in semver, but we'll also check for it and "Revision".
                if (installedVersion.Minor == requiredVersion.Minor && installedVersion.Build < requiredVersion.Build)
                    return false;
                if (installedVersion.Minor == requiredVersion.Minor && installedVersion.Build == requiredVersion.Build && installedVersion.Revision < requiredVersion.Revision)
                    return false;

                return true;
            }

            private static ResolveEventHandler GenerateModAssemblyResolver(EverestModuleMetadata meta)
                => (sender, args) => {
                    AssemblyName asmName = args?.Name == null ? null : new AssemblyName(args.Name);
                    if (string.IsNullOrEmpty(asmName?.Name))
                        return null;

                    if (!string.IsNullOrEmpty(meta.PathArchive)) {
                        string asmPath = asmName.Name + ".dll";
                        using (ZipFile zip = new ZipFile(meta.PathArchive)) {
                            foreach (ZipEntry entry in zip.Entries) {
                                if (entry.FileName == asmPath)
                                    using (MemoryStream stream = entry.ExtractStream())
                                        return Relinker.GetRelinkedAssembly(meta, stream);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(meta.PathDirectory)) {
                        string asmPath = Path.Combine(meta.PathDirectory, asmName.Name + ".dll");
                        if (File.Exists(asmPath))
                            using (FileStream stream = File.OpenRead(asmPath))
                                return Relinker.GetRelinkedAssembly(meta, stream);
                    }

                    return null;
                };

            private static void ApplyRelinkerHackfixes(EverestModuleMetadata meta) {
                if (meta.Name == "BGswitch" && meta.Version < new Version(0, 1, 0, 0)) {
                    /* I wish I knew what went wrong while PenguinOwl compiled that build...
                     * 
                     * For whoever is going to end up here in the future:
                     * PenguinOwl's "BG toggler" mod .dll is just... weird.
                     * Yet it somehow worked in the past when MonoMod's relinker
                     * wasn't as accurate and strict as it is now.
                     * 
                     * -ade
                     */
                    Relinker.Modder.PostProcessors += _FixBGswitch;
                }

            }

            private static void _FixBGswitch(MonoModder modder) {
                // The broken code is inside of Celeste.BGModeToggle::Setup
                TypeDefinition t_BGModeToggle = modder.Module.GetType("Celeste.BGModeToggle");
                if (t_BGModeToggle == null)
                    return;

                ILContext il = new ILContext(t_BGModeToggle.FindMethod("Setup"));
                ILCursor c = new ILCursor(il);

                // newobj Grid::.ctor(System.Single,System.Single,System.Boolean[,]) -> newobj Grid::.ctor(System.Single,System.Single,System.Boolean[0...,0...])
                c.Index = 0;
                while (c.TryGotoNext(i => i.MatchNewobj<Grid>())) {
                    MethodReference ctor = c.Next.Operand as MethodReference;
                    if (ctor == null)
                        continue;

                    ArrayType param = (ArrayType) ctor.Parameters[2].ParameterType;
                    param.Dimensions.Clear();
                    param.Dimensions.Add(new ArrayDimension(0, null));
                    param.Dimensions.Add(new ArrayDimension(0, null));
                }
            }

            private static void ApplyModHackfixes(EverestModuleMetadata meta, Assembly asm) {
                if (meta.Name == "BGswitch" && meta.Version < new Version(0, 1, 0, 0)) {
                    Relinker.Modder.PostProcessors -= _FixBGswitch;
                }

                if (meta.Name == "Prideline" && meta.Version < new Version(1, 0, 0, 0)) {
                    // Prideline 1.0.0 has got a hardcoded path to /ModSettings/Prideline.flag
                    Type t_PridelineModule = asm.GetType("Celeste.Mod.Prideline.PridelineModule");
                    FieldInfo f_CustomFlagPath = t_PridelineModule.GetField("CustomFlagPath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    f_CustomFlagPath.SetValue(null, Path.Combine(PathSettings, "modsettings-Prideline-Flag.celeste"));
                }

            }

        }
    }
}
