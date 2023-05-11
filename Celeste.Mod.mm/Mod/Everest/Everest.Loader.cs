using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using Ionic.Zip;
using MAB.DotIgnore;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

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
            /// The path to the Everest /Mods/temporaryblacklist.txt file.
            /// </summary>
            public static string PathTemporaryBlacklist { get; internal set; }
            internal static string NameTemporaryBlacklist;
            internal static List<string> _TemporaryBlacklist;
            /// <summary>
            /// The currently loaded mod temporary blacklist.
            /// </summary>
            public static ReadOnlyCollection<string> TemporaryBlacklist => _TemporaryBlacklist?.AsReadOnly();

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

            private static bool enforceOptionalDependencies;

            internal static HashSet<string> FilesWithMetadataLoadFailures = new HashSet<string>();

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

            internal static FileSystemWatcher Watcher;

            internal static event Action<string, EverestModuleMetadata> OnCrawlMod;

            public static bool AutoLoadNewMods { get; internal set; }

            public static bool ShouldLoadFile(string file) {
                if (CoreModule.Settings.WhitelistFullOverride ?? false) {
                    return Whitelist != null ? Whitelist.Contains(file) : (!Blacklist.Contains(file) && (TemporaryBlacklist == null || !TemporaryBlacklist.Contains(file)));
                } else {
                    return (Whitelist != null && Whitelist.Contains(file)) || (!Blacklist.Contains(file) && (TemporaryBlacklist == null || !TemporaryBlacklist.Contains(file)));
                }
            }

            internal static void LoadAuto() {
                Directory.CreateDirectory(PathMods = Path.Combine(PathEverest, "Mods"));
                Directory.CreateDirectory(PathCache = Path.Combine(PathMods, "Cache"));

                PathBlacklist = Path.Combine(PathMods, "blacklist.txt");
                if (File.Exists(PathBlacklist)) {
                    _Blacklist = File.ReadAllLines(PathBlacklist).Select(l => (l.StartsWith("#") ? "" : l).Trim()).ToList();
                } else {
                    using (StreamWriter writer = File.CreateText(PathBlacklist)) {
                        writer.WriteLine("# This is the blacklist. Lines starting with # are ignored.");
                        writer.WriteLine("# Mod folders and archives listed in this file will be disabled.");
                        writer.WriteLine("ExampleFolder");
                        writer.WriteLine("SomeMod.zip");
                    }
                }
                if (!string.IsNullOrEmpty(NameTemporaryBlacklist)) {
                    PathTemporaryBlacklist = Path.Combine(PathMods, NameTemporaryBlacklist);
                    if (File.Exists(PathTemporaryBlacklist)) {
                        _TemporaryBlacklist = File.ReadAllLines(PathTemporaryBlacklist).Select(l => (l.StartsWith("#") ? "" : l).Trim()).ToList();
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
                        writer.WriteLine("# If you put the name of a mod zip in this file, it won't be auto-updated and it won't show update notifications on the title screen.");
                        writer.WriteLine("SomeMod.zip");
                    }
                }

                Stopwatch watch = Stopwatch.StartNew();

                enforceOptionalDependencies = true;

                string[] files = Directory.GetFiles(PathMods);
                for (int i = 0; i < files.Length; i++) {
                    string file = Path.GetFileName(files[i]);
                    if (!file.EndsWith(".zip") || !ShouldLoadFile(file))
                        continue;
                    LoadZip(Path.Combine(PathMods, file));
                }

                files = Directory.GetDirectories(PathMods);
                for (int i = 0; i < files.Length; i++) {
                    string file = Path.GetFileName(files[i]);
                    if (file == "Cache" || !ShouldLoadFile(file))
                        continue;
                    LoadDir(Path.Combine(PathMods, file));
                }

                enforceOptionalDependencies = false;
                Logger.Log(LogLevel.Info, "loader", "Loading mods with unsatisfied optional dependencies (if any)");
                Everest.CheckDependenciesOfDelayedMods();

                watch.Stop();
                Logger.Log(LogLevel.Verbose, "loader", $"ALL MODS LOADED IN {watch.ElapsedMilliseconds}ms");

                try {
                    Watcher = new FileSystemWatcher {
                        Path = PathMods,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                    };

                    Watcher.Created += LoadAutoUpdated;

                    Watcher.EnableRaisingEvents = true;
                    AutoLoadNewMods = true;
                } catch (Exception e) {
                    Logger.Log(LogLevel.Warn, "loader", $"Failed watching folder: {PathMods}");
                    e.LogDetailed();
                    Watcher?.Dispose();
                    Watcher = null;
                }
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
                if (!Flags.SupportRuntimeMods) {
                    Logger.Log(LogLevel.Warn, "loader", "Loader disabled!");
                    return;
                }

                if (!File.Exists(archive)) // Relative path? Let's just make it absolute.
                    archive = Path.Combine(PathMods, archive);
                if (!File.Exists(archive)) // It just doesn't exist.
                    return;

                Logger.Log(LogLevel.Verbose, "loader", $"Loading mod .zip: {archive}");

                EverestModuleMetadata[] multimetas = null;

                IgnoreList ignoreList = null;

                bool metaParsed = false;

                using (ZipFile zip = new ZipFile(archive)) {
                    foreach (ZipEntry entry in zip.Entries) {
                        if (entry.FileName is "everest.yaml" or "everest.yml") {
                            if (metaParsed) {
                                Logger.Log(LogLevel.Warn, "loader", $"{archive} has both everest.yaml and everest.yml. Ignoring {entry.FileName}.");
                                continue;
                            }
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
                                    Logger.Log(LogLevel.Warn, "loader", $"Failed parsing {entry.FileName} in {archive}: {e}");
                                    FilesWithMetadataLoadFailures.Add(archive);
                                }
                            }
                            metaParsed = true;
                            continue;
                        }
                        if (entry.FileName == ".everestignore") {
                            List<string> lines = new List<string>();
                            using (MemoryStream stream = entry.ExtractStream())
                            using (StreamReader reader = new StreamReader(stream)) {
                                while (!reader.EndOfStream) {
                                    lines.Add(reader.ReadLine());
                                }
                            }
                            ignoreList = new IgnoreList(lines);
                            continue;
                        }
                    }
                }

                ZipModContent contentMeta = new ZipModContent(archive);
                EverestModuleMetadata contentMetaParent = null;

                contentMeta.Ignore = ignoreList;

                Action contentCrawl = () => {
                    if (contentMeta == null)
                        return;
                    if (contentMetaParent != null) {
                        contentMeta.Mod = contentMetaParent;
                        contentMeta.Name = contentMetaParent.Name;
                    }
                    OnCrawlMod?.Invoke(archive, contentMetaParent);
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
                    EverestModuleMetadata meta = new EverestModuleMetadata() {
                        Name = "_zip_" + Path.GetFileNameWithoutExtension(archive),
                        VersionString = "0.0.0-dummy",
                        PathArchive = archive
                    };
                    meta.PostParse();
                    contentMetaParent = meta;
                    LoadModDelayed(meta, contentCrawl);
                }
            }

            /// <summary>
            /// Load a mod from a directory at runtime.
            /// </summary>
            /// <param name="dir">The path to the mod directory.</param>
            public static void LoadDir(string dir) {
                if (!Flags.SupportRuntimeMods) {
                    Logger.Log(LogLevel.Warn, "loader", "Loader disabled!");
                    return;
                }

                if (!Directory.Exists(dir)) // Relative path?
                    dir = Path.Combine(PathMods, dir);
                if (!Directory.Exists(dir)) // It just doesn't exist.
                    return;

                Logger.Log(LogLevel.Verbose, "loader", $"Loading mod directory: {dir}");

                EverestModuleMetadata[] multimetas = null;

                string metaPath = Path.Combine(dir, "everest.yaml");
                if (!File.Exists(metaPath)) {
                    metaPath = Path.Combine(dir, "everest.yml");
                } else if (File.Exists(Path.Combine(dir, "everest.yml"))) {
                    Logger.Log(LogLevel.Warn, "loader", $"{dir} has both everest.yaml and everest.yml. Ignoring everest.yml.");
                }
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
                            FilesWithMetadataLoadFailures.Add(dir);
                        }
                    }

                FileSystemModContent contentMeta = new FileSystemModContent(dir);
                EverestModuleMetadata contentMetaParent = null;

                string ignorePath = Path.Combine(dir, ".everestignore");
                if (File.Exists(ignorePath)) {
                    contentMeta.Ignore = new IgnoreList(ignorePath);
                }

                Action contentCrawl = () => {
                    if (contentMeta == null)
                        return;
                    if (contentMetaParent != null) {
                        contentMeta.Mod = contentMetaParent;
                        contentMeta.Name = contentMetaParent.Name;
                    }
                    OnCrawlMod?.Invoke(dir, contentMetaParent);
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
                    EverestModuleMetadata meta = new EverestModuleMetadata() {
                        Name = "_dir_" + Path.GetFileName(dir),
                        VersionString = "0.0.0-dummy",
                        PathDirectory = dir
                    };
                    meta.PostParse();
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
                if (!Flags.SupportRuntimeMods) {
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

                foreach (EverestModuleMetadata dep in meta.Dependencies)
                    if (!DependencyLoaded(dep)) {
                        Logger.Log(LogLevel.Info, "loader", $"Dependency {dep} of mod {meta} not loaded! Delaying.");
                        lock (Delayed) {
                            Delayed.Add(Tuple.Create(meta, callback));
                        }
                        return;
                    }

                foreach (EverestModuleMetadata dep in meta.OptionalDependencies) {
                    if (!DependencyLoaded(dep) && (enforceOptionalDependencies || Everest.Modules.Any(module => module.Metadata?.Name == dep.Name))) {
                        Logger.Log(LogLevel.Info, "loader", $"Optional dependency {dep} of mod {meta} not loaded! Delaying.");
                        lock (Delayed) {
                            Delayed.Add(Tuple.Create(meta, callback));
                        }
                        return;
                    }
                }

                callback?.Invoke();

                LoadMod(meta);
            }

            /// <summary>
            /// Load a mod .dll given its metadata at runtime. Doesn't load the mod content.
            /// </summary>
            /// <param name="meta">Metadata of the mod to load.</param>
            public static void LoadMod(EverestModuleMetadata meta) {
                if (!Flags.SupportRuntimeMods) {
                    Logger.Log(LogLevel.Warn, "loader", "Loader disabled!");
                    return;
                }

                if (meta == null)
                    return;

                using var _ = new ScopeFinalizer(() => Events.Everest.LoadMod(meta));

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
                if (!Flags.SupportRuntimeMods) {
                    Logger.Log(LogLevel.Warn, "loader", "Loader disabled!");
                    return;
                }

                if (string.IsNullOrEmpty(meta.PathArchive) && File.Exists(meta.DLL) && meta.SupportsCodeReload && CoreModule.Settings.CodeReload) {
                    try {
                        FileSystemWatcher watcher = meta.DevWatcher = new FileSystemWatcher {
                            Path = Path.GetDirectoryName(meta.DLL),
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                        };

                        watcher.Changed += (s, e) => {
                            if (e.FullPath != meta.DLL)
                                return;
                            ReloadModAssembly(s, e);
                            // FIXME: Should we dispose the old .dll watcher?
                        };

                        watcher.EnableRaisingEvents = true;
                    } catch (Exception e) {
                        Logger.Log(LogLevel.Warn, "loader", $"Failed watching folder: {Path.GetDirectoryName(meta.DLL)}");
                        e.LogDetailed();
                        meta.DevWatcher?.Dispose();
                        meta.DevWatcher = null;
                    }
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

                bool foundModule = false;
                for (int i = 0; i < types.Length; i++) {
                    Type type = types[i];

                    EverestModule mod = null;
                    try {
                        if (typeof(EverestModule).IsAssignableFrom(type) && !type.IsAbstract) {
                            foundModule = true;
                            if (!typeof(NullModule).IsAssignableFrom(type)) {
                                mod = (EverestModule) type.GetConstructor(_EmptyTypeArray).Invoke(_EmptyObjectArray);
                            }
                        }
                    } catch (TypeLoadException e) {
                        // The type likely depends on a base class from a missing optional dependency
                        Logger.Log(LogLevel.Warn, "loader", $"Skipping type '{type.FullName}' likely depending on optional dependency: {e}");
                    }

                   if (mod != null) {
                        mod.Metadata = meta;
                        mod.Register();
                   }
                }

                // Warn if we didn't find a module, as that could indicate an oversight from the developer
                if (!foundModule)
                    Logger.Log(LogLevel.Warn, "loader", "Assembly doesn't contain an EverestModule!");
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
                            Logger.Log(LogLevel.Verbose, "core", $"Saving save data slot {SaveData.Instance.FileSlot} for {module.Metadata} before reloading");
                            if (module.SaveDataAsync) {
                                module.WriteSaveData(SaveData.Instance.FileSlot, module.SerializeSaveData(SaveData.Instance.FileSlot));
                            } else {
#pragma warning disable CS0618 // Synchronous save / load IO is obsolete but some mods still override / use it.
                                if (CoreModule.Settings.SaveDataFlush ?? false)
                                    module.ForceSaveDataFlush++;
                                module.SaveSaveData(SaveData.Instance.FileSlot);
#pragma warning restore CS0618
                            }

                            if (SaveData.Instance.CurrentSession?.InArea ?? false) {
                                Logger.Log(LogLevel.Verbose, "core", $"Saving session slot {SaveData.Instance.FileSlot} for {module.Metadata} before reloading");
                                if (module.SaveDataAsync) {
                                    module.WriteSession(SaveData.Instance.FileSlot, module.SerializeSession(SaveData.Instance.FileSlot));
                                } else {
#pragma warning disable CS0618 // Synchronous save / load IO is obsolete but some mods still override / use it.
                                    if (CoreModule.Settings.SaveDataFlush ?? false)
                                        module.ForceSaveDataFlush++;
                                    module.SaveSession(SaveData.Instance.FileSlot);
#pragma warning restore CS0618
                                }
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
                if (!Flags.SupportRuntimeMods) {
                    return false;
                }

                // enforce dependencies.
                foreach (EverestModuleMetadata dep in meta.Dependencies)
                    if (!DependencyLoaded(dep))
                        return false;

                // enforce optional dependencies: an optional dependency is satisfied if either of these 2 applies:
                // - it is loaded (obviously)
                // - enforceOptionalDependencies = false and no version of the mod is loaded (if one is, it might be incompatible and cause issues)
                foreach (EverestModuleMetadata dep in meta.OptionalDependencies)
                    if (!DependencyLoaded(dep) && (enforceOptionalDependencies || Everest.Modules.Any(mod => mod.Metadata?.Name == dep.Name)))
                        return false;

                return true;
            }

            /// <summary>
            /// Checks if an dependency is loaded.
            /// Can be used by mods manually to f.e. activate / disable functionality.
            /// </summary>
            /// <param name="dep">Dependency to check for. Name and Version will be checked.</param>
            /// <returns>True if the dependency has already been loaded by Everest, false otherwise.</returns>
            public static bool DependencyLoaded(EverestModuleMetadata dep) =>
                TryGetDependency(dep, out EverestModule _);

            /// <summary>
            /// Fetch a dependency if it is loaded.
            /// Can be used by mods manually to f.e. activate / disable functionality.
            /// </summary>
            /// <param name="dep">Dependency to check for. Name and Version will be checked.</param>
            /// <param name="module">EverestModule for the dependency if found, null if not.</param>
            /// <returns>True if the dependency has already been loaded by Everest, false otherwise.</returns>
            public static bool TryGetDependency(EverestModuleMetadata dep, out EverestModule module) {
                string depName = dep.Name;
                Version depVersion = dep.Version;

                lock (_Modules) {
                    foreach (EverestModule other in _Modules) {
                        EverestModuleMetadata meta = other.Metadata;
                        if (meta.Name != depName)
                            continue;

                        Version version = meta.Version;
                        if (VersionSatisfiesDependency(depVersion, version)) {
                            module = other;
                            return true;
                        }
                    }
                }
                module = null;
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

            private static void ApplyModHackfixes(EverestModuleMetadata meta, Assembly asm) {
                // Feel free to keep this as a reminder on mod hackfixes or whatever. -jade
                /*
                if (meta.Name == "Prideline" && meta.Version < new Version(1, 0, 0, 0)) {
                    // Prideline 1.0.0 has got a hardcoded path to /ModSettings/Prideline.flag
                    Type t_PridelineModule = asm.GetType("Celeste.Mod.Prideline.PridelineModule");
                    FieldInfo f_CustomFlagPath = t_PridelineModule.GetField("CustomFlagPath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    f_CustomFlagPath.SetValue(null, Path.Combine(PathSettings, "modsettings-Prideline-Flag.celeste"));
                }
                */
            }

        }
    }
}