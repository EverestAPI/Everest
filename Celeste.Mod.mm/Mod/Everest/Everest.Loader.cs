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
using System.Diagnostics;
using Celeste.Mod.Core;

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

            internal static readonly Version _VersionInvalid = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
            internal static readonly Version _VersionMax = new Version(int.MaxValue, int.MaxValue);

            /// <summary>
            /// The path to the Everest /Mods/modoptionsorder.txt file.
            /// </summary>
            public static string PathModOptionsOrder { get; internal set; }
            internal static List<string> _ModOptionsOrder = new List<string>();
            /// <summary>
            /// The currently loaded mod mod options order.
            /// </summary>
            public static ReadOnlyCollection<string> ModOptionsOrder => _ModOptionsOrder?.AsReadOnly();

            /// <summary>
            /// The path to the Everest /Mods/updaterblacklist.txt file.
            /// </summary>
            public static string PathUpdaterBlacklist { get; internal set; }
            internal static List<string> _UpdaterBlacklist = new List<string>();
            /// <summary>
            /// The currently loaded mod updater blacklist.
            /// </summary>
            public static ReadOnlyCollection<string> UpdaterBlacklist => _UpdaterBlacklist?.AsReadOnly();

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

            /// <summary>
            /// When both mods in the same row with versions lower than in the row are present, yell at the user.
            /// </summary>
            internal static HashSet<Tuple<string, Version, string, Version>> PermanentConflictlist = new HashSet<Tuple<string, Version, string, Version>>() {

                // See above versioning note.

                // I'm sorry. -ade
                Tuple.Create("Nameguy's D-Sides", _VersionMax, "Monika's D-Sides", _VersionMax),
            };

            internal static FileSystemWatcher Watcher;

            public static bool AutoLoadNewMods { get; internal set; }

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
                PathModOptionsOrder = Path.Combine(PathMods, "modoptionsorder.txt");
                if (File.Exists(PathModOptionsOrder)) {
                    _ModOptionsOrder = File.ReadAllLines(PathModOptionsOrder).Select(l => (l.StartsWith("#") ? "" : l).Trim()).ToList();
                } else {
                    using (StreamWriter writer = File.CreateText(PathModOptionsOrder)) {
                        writer.WriteLine("# This is the Mod Options order file. Lines starting with # are ignored.");
                        writer.WriteLine("# Mod folders and archives in this file will be displayed in the same order in the Mod Options menu.");
                        writer.WriteLine("# To define the position of the \"Everest Core\" options, put \"Everest\" on a line.");
                        writer.WriteLine("ExampleFolder");
                        writer.WriteLine("SomeMod.zip");
                    }
                }

                PathUpdaterBlacklist = Path.Combine(PathMods, "updaterblacklist.txt");
                if (File.Exists(PathUpdaterBlacklist)) {
                    _UpdaterBlacklist = File.ReadAllLines(PathUpdaterBlacklist).Select(l => (l.StartsWith("#") ? "" : l).Trim()).ToList();
                } else {
                    using (StreamWriter writer = File.CreateText(PathUpdaterBlacklist)) {
                        writer.WriteLine("# This is the Updater Blacklist. Lines starting with # are ignored.");
                        writer.WriteLine("# Put the name of a mod zip here to prevent it from being auto-updated and to show update notifications on the title screen.");
                        writer.WriteLine("SomeMod.zip");
                    }
                }

                if (Flags.IsDisabled)
                    return;

                Stopwatch watch = Stopwatch.StartNew();

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

                watch.Stop();
                Logger.Log(LogLevel.Verbose, "loader", $"ALL MODS LOADED IN {watch.ElapsedMilliseconds}ms");

                Watcher = new FileSystemWatcher {
                    Path = PathMods,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                };

                Watcher.Created += LoadAutoUpdated;

                Watcher.EnableRaisingEvents = true;
                AutoLoadNewMods = true;
            }

            private static void LoadAutoUpdated(object source, FileSystemEventArgs e) {
                if (!AutoLoadNewMods)
                    return;

                Logger.Log(LogLevel.Info, "loader", $"Possible new mod container: {e.FullPath}");
                QueuedTaskHelper.Do("LoadAutoUpdated:" + e.FullPath, () => AssetReloadHelper.Do($"{Dialog.Clean("ASSETRELOADHELPER_LOADINGNEWMOD")} {Path.GetFileName(e.FullPath)}", () => MainThreadHelper.Do(() => {
                    if (Directory.Exists(e.FullPath))
                        LoadDir(e.FullPath);
                    else if (e.FullPath.EndsWith(".zip"))
                        LoadZip(e.FullPath);
                    ((patch_OuiMainMenu) (AssetReloadHelper.ReturnToScene as Overworld)?.GetUI<OuiMainMenu>())?.RebuildMainAndTitle();
                })));
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
                    }
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

                if (Modules.Any(module => module.Metadata.Name == meta.Name)) {
                    Logger.Log(LogLevel.Warn, "loader", $"Mod {meta.Name} already loaded!");
                    return;
                }

                if (PermanentBlacklist.TryGetValue(meta.Name, out Version minver) && meta.Version < minver) {
                    Logger.Log(LogLevel.Warn, "loader", $"Mod {meta} permanently blacklisted by Everest!");
                    return;
                }

                Tuple<string, Version, string, Version> conflictRow = PermanentConflictlist.FirstOrDefault(row =>
                    (meta.Name == row.Item1 && meta.Version < row.Item2 && (_Modules.FirstOrDefault(other => other.Metadata.Name == row.Item3)?.Metadata.Version ?? _VersionInvalid) < row.Item4) ||
                    (meta.Name == row.Item3 && meta.Version < row.Item4 && (_Modules.FirstOrDefault(other => other.Metadata.Name == row.Item1)?.Metadata.Version ?? _VersionInvalid) < row.Item2)
                );
                if (conflictRow != null) {
                    throw new Exception($"CONFLICTING MODS: {conflictRow.Item1} VS {conflictRow.Item3}");
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

                // Load the actual assembly.
                Assembly asm = null;
                if (!string.IsNullOrEmpty(meta.PathArchive)) {
                    bool returnEarly = false;
                    using (ZipFile zip = new ZipFile(meta.PathArchive)) {
                        foreach (ZipEntry entry in zip.Entries) {
                            string entryName = entry.FileName.Replace('\\', '/');
                            if (entryName == meta.DLL) {
                                using (MemoryStream stream = entry.ExtractStream())
                                    asm = Relinker.GetRelinkedAssembly(meta, Path.GetFileNameWithoutExtension(meta.DLL), stream);
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
                            asm = Relinker.GetRelinkedAssembly(meta, Path.GetFileNameWithoutExtension(meta.DLL), stream);
                    }

                    if (File.Exists(Path.Combine(meta.PathDirectory, "main.lua"))) {
                        new LuaModule(meta).Register();
                        return;
                    }
                }

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

                if (string.IsNullOrEmpty(meta.PathArchive) && File.Exists(meta.DLL) && meta.SupportsCodeReload && CoreModule.Settings.CodeReload) {
                    FileSystemWatcher watcher = meta.DevWatcher = new FileSystemWatcher {
                        Path = Path.GetDirectoryName(meta.DLL),
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    };

                    watcher.Changed += (s, e) => {
                        if (e.FullPath != meta.DLL)
                            return;
                        ReloadModAssembly(s, e);
                    };

                    watcher.EnableRaisingEvents = true;
                }

                ApplyModHackfixes(meta, asm);

                Content.Crawl(new AssemblyModContent(asm) {
                    Mod = meta,
                    Name = meta.Name
                });

                Type[] types;
                try {
                    types = asm.GetTypesSafe();
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

            internal static void ReloadModAssembly(object source, FileSystemEventArgs e, bool retrying = false) {
                if (!File.Exists(e.FullPath))
                    return;

                Logger.Log(LogLevel.Info, "loader", $"Reloading mod assembly: {e.FullPath}");
                QueuedTaskHelper.Do("ReloadModAssembly:" + e.FullPath, () => {
                    EverestModule module = _Modules.FirstOrDefault(m => m.Metadata.DLL == e.FullPath);
                    if (module == null)
                        return;

                    AssetReloadHelper.Do($"{Dialog.Clean("ASSETRELOADHELPER_RELOADINGMODASSEMBLY")} {Path.GetFileName(e.FullPath)}", () => {
                        Assembly asm = null;
                        using (FileStream stream = File.OpenRead(e.FullPath))
                            asm = Relinker.GetRelinkedAssembly(module.Metadata, Path.GetFileNameWithoutExtension(e.FullPath), stream);

                        if (asm == null) {
                            if (!retrying) {
                                // Retry.
                                QueuedTaskHelper.Do("ReloadModAssembly:" + e.FullPath, () => {
                                    ReloadModAssembly(source, e, true);
                                });
                            }
                            return;
                        }

                        ((FileSystemWatcher) source).Dispose();

                        // be sure to save this module's save data and session before reloading it, so that they are not lost.
                        if (SaveData.Instance != null) {
                            Logger.Log("core", $"Saving save data slot {SaveData.Instance.FileSlot} for {module.Metadata} before reloading");
                            module.SaveSaveData(SaveData.Instance.FileSlot);

                            if (SaveData.Instance.CurrentSession?.InArea ?? false) {
                                Logger.Log("core", $"Saving session slot {SaveData.Instance.FileSlot} for {module.Metadata} before reloading");
                                module.SaveSession(SaveData.Instance.FileSlot);
                            }
                        }

                        Unregister(module);
                        LoadModAssembly(module.Metadata, asm);
                    });
                    AssetReloadHelper.ReloadLevel();
                });
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

                lock (_Modules) {
                    foreach (EverestModule other in _Modules) {
                        EverestModuleMetadata meta = other.Metadata;
                        if (meta.Name != depName)
                            continue;

                        Version version = meta.Version;
                        return VersionSatisfiesDependency(depVersion, version);
                    }
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
                    AssemblyName name = args?.Name == null ? null : new AssemblyName(args.Name);
                    if (string.IsNullOrEmpty(name?.Name))
                        return null;

                    string path = name.Name + ".dll";
                    if (!string.IsNullOrEmpty(meta.DLL))
                        path = Path.Combine(Path.GetDirectoryName(meta.DLL), path);

                    if (!string.IsNullOrEmpty(meta.PathArchive)) {
                        string zipPath = path.Replace('\\', '/');
                        using (ZipFile zip = new ZipFile(meta.PathArchive)) {
                            foreach (ZipEntry entry in zip.Entries) {
                                if (entry.FileName == zipPath)
                                    using (MemoryStream stream = entry.ExtractStream())
                                        return Relinker.GetRelinkedAssembly(meta, Path.GetFileNameWithoutExtension(zipPath), stream);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(meta.PathDirectory)) {
                        string filePath = path;
                        if (!File.Exists(filePath))
                            path = Path.Combine(meta.PathDirectory, filePath);
                        if (File.Exists(filePath))
                            using (FileStream stream = File.OpenRead(filePath))
                                return Relinker.GetRelinkedAssembly(meta, Path.GetFileNameWithoutExtension(filePath), stream);
                    }

                    return null;
                };

            private static void ApplyModHackfixes(EverestModuleMetadata meta, Assembly asm) {
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
