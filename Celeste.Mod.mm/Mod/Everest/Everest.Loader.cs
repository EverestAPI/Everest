using Celeste.Mod.Backdrops;
using Celeste.Mod.Core;
using Celeste.Mod.Entities;
using Celeste.Mod.Helpers;
using Celeste.Mod.Registry;
using MAB.DotIgnore;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

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
            internal static HashSet<string> _Blacklist = new HashSet<string>();
            /// <summary>
            /// The currently loaded mod blacklist.
            /// </summary>
            public static IReadOnlyCollection<string> Blacklist => _Blacklist.ToImmutableHashSet();

            /// <summary>
            /// The path to the Everest /Mods/favorites.txt file.
            /// </summary>
            public static string PathFavorites { get; internal set; }
            internal static HashSet<string> Favorites = new HashSet<string>();

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

            private sealed class PendingStartupMod {
                internal EverestModuleMetadata Metadata;
                internal Action ContentCrawl;
                internal int DiscoveryIndex;
                internal int ContentCommitIndex = -1;
                internal int SchedulingPriority = int.MaxValue;
                internal double EstimatedLoadMilliseconds;
                internal double CriticalPathMilliseconds;
                internal int TimingSamples;
                internal bool PrefersReservedWorker;
                internal bool RequiresExclusiveExecution;
            }

            private sealed class StartupLoadResult {
                internal PendingStartupMod Pending;
                internal bool Succeeded;
                internal Exception Error;
            }

            private static readonly List<PendingStartupMod> PendingStartupMods = new List<PendingStartupMod>();
            private static bool collectingStartupMods;
            internal static bool IsBatchLoading { get; private set; }
            internal static readonly object ModuleRegistrationFinalizeLock = new object();
            private static readonly object ContentCrawlLock = new object();
            private static readonly object AssemblyProcessingLock = new object();

            private static bool ParallelStartupEnabled =>
                Environment.GetEnvironmentVariable("EVEREST_PARALLEL_LOAD") != "0"
                && !File.Exists(Path.Combine(AppContext.BaseDirectory, ".everest-disable-parallel-load"));

            private static bool ILHookStartupTransactionEnabled =>
                ParallelStartupEnabled
                && Environment.GetEnvironmentVariable("EVEREST_ILHOOK_STARTUP_TRANSACTION") != "0"
                && !File.Exists(Path.Combine(AppContext.BaseDirectory, ".everest-disable-ilhook-startup-transaction"));

            private static bool ProfileGuidedReorderingEnabled =>
                LoaderTimingCache.Enabled
                && Environment.GetEnvironmentVariable("EVEREST_LOADER_PGO_REORDER") != "0"
                && !File.Exists(Path.Combine(AppContext.BaseDirectory, ".everest-disable-loader-pgo-reorder"));

            // Used by the runtime-detour safety gate. Arbitrary mods can create IL hooks
            // from EverestModule.Load, but MonoMod only serializes hooks per target method.
            // Parallel startup needs a process-wide gate around IL manipulator execution.
            internal static bool IsParallelStartupLoading => IsBatchLoading && ParallelStartupEnabled;

            private static int ParallelStartupDegree {
                get {
                    if (int.TryParse(Environment.GetEnvironmentVariable("EVEREST_PARALLEL_LOAD_DEGREE"), out int configured))
                        return Math.Clamp(configured, 1, 32);
                    // Warm cached loading is mostly independent assembly materialization,
                    // type discovery and mod code. After removing the global relinker lock,
                    // 16 workers gave the best stable result on the 24-thread reference
                    // machine; 24-32 started losing time to runtime-loader and GC contention.
                    return Math.Clamp(Environment.ProcessorCount * 2 / 3, 8, 16);
                }
            }

            private static int ILHookFlushDegree {
                get {
                    if (int.TryParse(Environment.GetEnvironmentVariable("EVEREST_ILHOOK_FLUSH_DEGREE"), out int configured))
                        return Math.Clamp(configured, 1, 16);
                    // Flush targets are independent except for per-mod manipulator gates.
                    // Twelve workers were the best measured point on the 24-thread reference
                    // machine; sixteen added enough JIT/loader contention to regress.
                    return Math.Clamp(Environment.ProcessorCount / 2, 4, 12);
                }
            }

            private static object GetILHookManipulatorOwnerKey(MonoMod.Cil.ILContext.Manipulator manipulator) {
                Assembly ownerAssembly = manipulator.Method.DeclaringType?.Assembly;
                if (ownerAssembly != null
                    && AssemblyLoadContext.GetLoadContext(ownerAssembly) is EverestModuleAssemblyContext context
                    && context.ModuleMeta?.Name != null)
                    return context.ModuleMeta.Name;
                return ownerAssembly;
            }

            // Modules listed via EVEREST_PARALLEL_LOAD_EXCLUSIVE_MODS run alone at a
            // quiescent point. This is a manual escape hatch for mods that are unsafe in
            // parallel for reasons Everest cannot fix (process-wide state, unguarded
            // global collections, ...). Known races such as enumerating Content.Mods while
            // assembly content is crawled are fixed at the source, so no built-in
            // deny-list is needed.
            private static bool RequiresExclusiveStartupExecution(EverestModuleMetadata meta) {
                if (meta?.Name == null)
                    return false;

                string configured = Environment.GetEnvironmentVariable("EVEREST_PARALLEL_LOAD_EXCLUSIVE_MODS");
                if (string.IsNullOrWhiteSpace(configured))
                    return false;
                return configured.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(name => string.Equals(name.Trim(), meta.Name, StringComparison.Ordinal));
            }

            private static bool enforceOptionalDependencies;

            internal static HashSet<string> FilesWithMetadataLoadFailures = new HashSet<string>();
            internal static HashSet<EverestModuleMetadata> ModsWithAssemblyLoadFailures = new HashSet<EverestModuleMetadata>();

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
                LoaderProfiler.Reset();
                Directory.CreateDirectory(PathMods = Path.Combine(PathEverest, "Mods"));
                Directory.CreateDirectory(PathCache = Path.Combine(PathMods, "Cache"));

                PathBlacklist = Path.Combine(PathMods, "blacklist.txt");
                if (File.Exists(PathBlacklist)) {
                    _Blacklist = File.ReadAllLines(PathBlacklist).Select(l => (l.StartsWith("#") ? "" : l).Trim()).ToHashSet<string>();
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

                PathFavorites = Path.Combine(PathMods, "favorites.txt");
                if (File.Exists(PathFavorites)) {
                    Favorites = new HashSet<string>(File.ReadAllLines(PathFavorites).Select(l => (l.StartsWith("#") ? "" : l).Trim()));
                } else {
                    using (StreamWriter writer = File.CreateText(PathFavorites)) {
                        writer.WriteLine("# This is the favorites list. Lines starting with # are ignored.");
                    }
                }

                Stopwatch watch = Stopwatch.StartNew();

                enforceOptionalDependencies = true;

                string[] files;
                string[] dirs;
                using (LoaderProfiler.Measure("enumerate-containers")) {
                    files = Directory
                        .GetFiles(PathMods)
                        .OrderBy(f => f) //Prevent inode loading jank
                        .Select(Path.GetFileName)
                        .Where(file => file.EndsWith(".zip") && ShouldLoadFile(file))
                        .ToArray();

                    dirs = Directory
                        .GetDirectories(PathMods)
                        .OrderBy(f => f) //Prevent inode loading jank
                        .Select(Path.GetFileName)
                        .Where(file => file != "Cache" && ShouldLoadFile(file))
                        .ToArray();
                }

                EverestSplashHandler.SetSplashLoadingModCount(files.Length + dirs.Length);

                LoaderMetadataCache.Initialize(PathCache);
                LoaderTimingCache.Initialize(PathCache);
                LoaderChecksumCache.Initialize(PathCache);
                LoaderMetadataCache.Prefetch(files.Select(file => Path.Combine(PathMods, file)));

                PendingStartupMods.Clear();
                collectingStartupMods = true;
                foreach (string file in files) {
                    LoadZip(Path.Combine(PathMods, file));
                }
                foreach (string dir in dirs) {
                    LoadDir(Path.Combine(PathMods, dir));
                }
                collectingStartupMods = false;
                LoaderMetadataCache.Flush();

                enforceOptionalDependencies = false;
                Logger.Info("loader", ParallelStartupEnabled
                    ? $"Parallel startup scheduler enabled (dynamic DAG, max degree {ParallelStartupDegree})."
                    : "Parallel startup scheduler disabled.");
                try {
                    using (LoaderProfiler.Measure("dependency-plan-load"))
                        LoadPendingStartupMods();
                } finally {
                    LoaderTimingCache.Flush();
                    LoaderChecksumCache.Flush();
                }

                EverestSplashHandler.AllModsLoaded();

                watch.Stop();
                LoaderProfiler.Report(watch.ElapsedMilliseconds);
                Logger.Verbose("loader", $"ALL MODS LOADED IN {watch.ElapsedMilliseconds}ms");
                Logger.Info("loader", $"Loaded {Everest._Modules.Count} modules");

                try {
                    Watcher = new FileSystemWatcher {
                        Path = PathMods,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                    };

                    Watcher.Created += LoadAutoUpdated;
                    Watcher.Error += WatcherError;

                    Watcher.EnableRaisingEvents = true;
                    AutoLoadNewMods = true;
                } catch (Exception e) {
                    Logger.Warn("loader", $"Failed watching folder: {PathMods}");
                    Logger.LogDetailed(e);
                    Watcher?.Dispose();
                    Watcher = null;
                }
            }

            private static void LoadAutoUpdated(object source, FileSystemEventArgs e) {
                if (!AutoLoadNewMods)
                    return;

                Logger.Info("loader", $"Possible new mod container: {e.FullPath}");
                QueuedTaskHelperV2.Do("LoadAutoUpdated:" + e.FullPath, () => AssetReloadHelper.Do($"{Dialog.Clean("ASSETRELOADHELPER_LOADINGNEWMOD")} {Path.GetFileName(e.FullPath)}", () => MainThreadHelper.Schedule(() => {
                    if (Directory.Exists(e.FullPath))
                        LoadDir(e.FullPath);
                    else if (e.FullPath.EndsWith(".zip"))
                        LoadZip(e.FullPath);
                    ((patch_OuiMainMenu) (AssetReloadHelper.ReturnToScene as Overworld)?.GetUI<OuiMainMenu>())?.NeedsRebuild();
                })));
            }

            private static void WatcherError(object source, ErrorEventArgs e) {
                Logger.Error("loader", $"Error while watching \"{PathMods}\" for changes. Updates will no longer be detected.");
                Logger.LogDetailed(e.GetException());
            }

            /// <summary>
            /// Load a mod from a .zip archive at runtime.
            /// </summary>
            /// <param name="archive">The path to the mod .zip archive.</param>
            public static void LoadZip(string archive) {
                if (!File.Exists(archive)) // Relative path? Let's just make it absolute.
                    archive = Path.Combine(PathMods, archive);
                if (!File.Exists(archive)) { // It just doesn't exist.
                    EverestSplashHandler.IncreaseLoadedModCount(null); // Increase the splash count anyway, since it was detected as an entry
                    return;
                }

                Logger.Verbose("loader", $"Loading mod .zip: {archive}");

                EverestModuleMetadata[] multimetas = null;
                IgnoreList ignoreList = null;
                LoaderMetadataCache.ZipMetadata cached;
                using (LoaderProfiler.Measure("zip-metadata-io")) {
                    if (!LoaderMetadataCache.TryGet(archive, out cached)) {
                        cached = LoaderMetadataCache.ReadArchive(archive);
                        LoaderMetadataCache.Store(archive, cached);
                    }
                }
                if (cached.HasBothMetadataFiles)
                    Logger.Warn("loader", $"{archive} has both everest.yaml and everest.yml. Ignoring the second file.");
                if (cached.IgnoreLines != null)
                    ignoreList = new IgnoreList(cached.IgnoreLines);
                if (!string.IsNullOrEmpty(cached.Yaml)) {
                    try {
                        using StringReader reader = new StringReader(cached.Yaml);
                        using (LoaderProfiler.Measure("yaml-deserialize"))
                            multimetas = YamlHelper.Deserializer.Deserialize<EverestModuleMetadata[]>(reader);
                        foreach (EverestModuleMetadata multimeta in multimetas) {
                            multimeta.PathArchive = archive;
                            multimeta.PostParse();
                        }
                    } catch (Exception e) {
                        Logger.Warn("loader", $"Failed parsing everest metadata in {archive}: {e}");
                        FilesWithMetadataLoadFailures.Add(archive);
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
                    using (LoaderProfiler.Measure("content-crawl"))
                        Content.Crawl(contentMeta);
                    contentMeta = null;
                };

                if (multimetas != null) {
                    // When estimating the total mod count for the splash it is assumed that there will be exactly one
                    // ModuleMetadata per filesystem entry, which is a valid assumption most of the time, but very few
                    // mods do have multiple ModuleMetadatas in its everest.yaml, that's why we increase the total count
                    // late here when we realize that one may contain multiple
                    EverestSplashHandler.IncreaseTotalModCount(multimetas.Length-1);
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
                if (!Directory.Exists(dir)) // Relative path?
                    dir = Path.Combine(PathMods, dir);
                if (!Directory.Exists(dir)) { // It just doesn't exist.
                    EverestSplashHandler.IncreaseLoadedModCount(null); // Increase the splash count anyway, since it was detected as an entry
                    return;
                }

                Logger.Verbose("loader", $"Loading mod directory: {dir}");

                EverestModuleMetadata[] multimetas = null;

                string metaPath = Path.Combine(dir, "everest.yaml");
                if (!File.Exists(metaPath)) {
                    metaPath = Path.Combine(dir, "everest.yml");
                } else if (File.Exists(Path.Combine(dir, "everest.yml"))) {
                    Logger.Warn("loader", $"{dir} has both everest.yaml and everest.yml. Ignoring everest.yml.");
                }
                if (File.Exists(metaPath))
                    using (StreamReader reader = new StreamReader(metaPath)) {
                        try {
                            if (!reader.EndOfStream) {
                                using (LoaderProfiler.Measure("yaml-deserialize"))
                                    multimetas = YamlHelper.Deserializer.Deserialize<EverestModuleMetadata[]>(reader);
                                foreach (EverestModuleMetadata multimeta in multimetas) {
                                    multimeta.PathDirectory = dir;
                                    multimeta.PostParse();
                                }
                            }
                        } catch (Exception e) {
                            Logger.Warn("loader", $"Failed parsing everest.yaml in {dir}: {e}");
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
                    using (LoaderProfiler.Measure("content-crawl"))
                        Content.Crawl(contentMeta);
                    contentMeta = null;
                };

                if (multimetas != null) {
                    // When estimating the total mod count for the splash it is assumed that there will be exactly one
                    // ModuleMetadata per filesystem entry, which is a valid assumption most of the time, but very few
                    // mods do have multiple ModuleMetadatas in its everest.yaml, that's why we increase the total count
                    // late here when we realize that one may contain multiple
                    EverestSplashHandler.IncreaseTotalModCount(multimetas.Length-1);
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
                if (meta == null) {
                    callback?.Invoke();
                    return;
                }

                if (collectingStartupMods) {
                    PendingStartupMods.Add(new PendingStartupMod {
                        Metadata = meta,
                        ContentCrawl = callback,
                        DiscoveryIndex = PendingStartupMods.Count
                    });
                    return;
                }

                if (Modules.Any(module => module.Metadata.Name == meta.Name)) {
                    Logger.Warn("loader", $"Mod {meta.Name} already loaded!");
                    return;
                }

                foreach (EverestModuleMetadata dep in meta.Dependencies)
                    if (!DependencyLoaded(dep)) {
                        Logger.Info("loader", $"Dependency {dep} of mod {meta} not loaded! Delaying.");
                        lock (Delayed) {
                            Delayed.Add(Tuple.Create(meta, callback));
                        }
                        return;
                    }

                foreach (EverestModuleMetadata dep in meta.OptionalDependencies) {
                    if (!DependencyLoaded(dep) && (enforceOptionalDependencies || Everest.Modules.Any(module => module.Metadata?.Name == dep.Name))) {
                        Logger.Info("loader", $"Optional dependency {dep} of mod {meta} not loaded! Delaying.");
                        lock (Delayed) {
                            Delayed.Add(Tuple.Create(meta, callback));
                        }
                        return;
                    }
                }

                callback?.Invoke();

                EverestSplashHandler.IncreaseLoadedModCount(meta.Name);
                LoadMod(meta);
            }

            private static void LoadPendingStartupMods() {
                // The old startup loader repeatedly rescanned the delayed list after every module.
                // Build the dependency graph once instead, preserving discovery order between
                // otherwise independent modules.
                Dictionary<string, PendingStartupMod> candidates = new Dictionary<string, PendingStartupMod>(StringComparer.Ordinal);
                foreach (PendingStartupMod pending in PendingStartupMods) {
                    if (!candidates.TryAdd(pending.Metadata.Name, pending))
                        Logger.Warn("loader", $"Mod {pending.Metadata.Name} already discovered; keeping the first container.");
                }

                HashSet<PendingStartupMod> valid = new HashSet<PendingStartupMod>(candidates.Values);
                bool changed;
                do {
                    changed = false;
                    foreach (PendingStartupMod pending in valid.ToArray()) {
                        foreach (EverestModuleMetadata dependency in pending.Metadata.Dependencies) {
                            if (DependencyLoaded(dependency))
                                continue;
                            if (!candidates.TryGetValue(NormalizeDependencyName(dependency.Name), out PendingStartupMod candidate)
                                || !valid.Contains(candidate)
                                || !VersionSatisfiesDependency(dependency.Version, candidate.Metadata.Version)) {
                                valid.Remove(pending);
                                changed = true;
                                Logger.Warn("loader", $"Dependency {dependency} of mod {pending.Metadata} is unavailable; skipping it.");
                                break;
                            }
                        }
                    }
                } while (changed);

                Dictionary<PendingStartupMod, List<(PendingStartupMod Target, bool Optional)>> outgoing =
                    valid.ToDictionary(node => node, _ => new List<(PendingStartupMod, bool)>());
                Dictionary<PendingStartupMod, int> indegree = valid.ToDictionary(node => node, _ => 0);

                foreach (PendingStartupMod pending in valid) {
                    AddDependencyEdges(pending, pending.Metadata.Dependencies, optional: false, candidates, valid, outgoing, indegree);
                    AddDependencyEdges(pending, pending.Metadata.OptionalDependencies, optional: true, candidates, valid, outgoing, indegree);
                }

                AssignStableContentCommitIndices(valid, outgoing, indegree);
                AssignProfileGuidedSchedulingPriorities(valid, outgoing);
                CommitContainerContentInStableOrder(valid);

                PriorityQueue<PendingStartupMod, int> ready = new PriorityQueue<PendingStartupMod, int>();
                foreach ((PendingStartupMod node, int degree) in indegree)
                    if (degree == 0) {
                        ready.Enqueue(node, node.SchedulingPriority);
                        LoaderProfiler.Mark($"startup-node-ready/{node.Metadata.Name}");
                    }

                HashSet<PendingStartupMod> loaded = new HashSet<PendingStartupMod>();
                MonoMod.RuntimeDetour.ILHookTransaction ilHookTransaction = null;
                if (ILHookStartupTransactionEnabled) {
                    ilHookTransaction = MonoMod.RuntimeDetour.ILHookTransaction.Begin();
                    Logger.Info("loader", "Experimental startup ILHook transaction enabled; hooks will be committed once per target method.");
                }
                IsBatchLoading = true;
                try {
                    DrainReadyQueue(ready, loaded, outgoing, indegree);

                    if (loaded.Count < valid.Count) {
                        // Optional dependency cycles are legal. Drop only optional edges between
                        // remaining nodes and continue Kahn's algorithm with the hard-dependency DAG.
                        HashSet<PendingStartupMod> remaining = valid.Where(node => !loaded.Contains(node)).ToHashSet();
                        foreach (PendingStartupMod node in remaining)
                            indegree[node] = 0;
                        foreach (PendingStartupMod source in remaining)
                            foreach ((PendingStartupMod target, bool optional) in outgoing[source])
                                if (!optional && remaining.Contains(target))
                                    indegree[target]++;
                        foreach (PendingStartupMod node in remaining)
                            if (indegree[node] == 0) {
                                ready.Enqueue(node, node.SchedulingPriority);
                                LoaderProfiler.Mark($"startup-node-ready/{node.Metadata.Name}");
                            }
                        DrainReadyQueue(ready, loaded, outgoing, indegree, ignoreOptionalEdges: true);
                    }

                    // Assembly content was crawled on workers but intentionally kept out of
                    // Content.Mods during the parallel phase. Publish it now that no module
                    // Load() is running, in the same stable topological order used for
                    // container content and ILHook application.
                    using (LoaderProfiler.Measure("content-assembly-register"))
                    foreach (PendingStartupMod pending in valid
                        .Where(node => node.Metadata.AssemblyContent != null)
                        .OrderBy(node => node.ContentCommitIndex))
                        Content.RegisterMod(pending.Metadata.AssemblyContent);

                    if (ilHookTransaction != null) {
                        int pendingHooks = ilHookTransaction.PendingCount;
                        (int hooks, int targets) result;
                        using (LoaderProfiler.Measure("ilhook-transaction-flush"))
                            result = ilHookTransaction.Flush(ILHookFlushDegree, GetILHookManipulatorOwnerKey);
                        Logger.Info("loader", $"Startup ILHook transaction committed {result.hooks}/{pendingHooks} hooks across {result.targets} target methods (target degree {ILHookFlushDegree}, per-mod manipulator serialization).");
                    }
                } finally {
                    ilHookTransaction?.Dispose();
                    IsBatchLoading = false;
                }

                foreach (PendingStartupMod pending in valid.OrderBy(node => node.DiscoveryIndex))
                    if (!loaded.Contains(pending))
                        Logger.Warn("loader", $"Hard dependency cycle prevented loading mod {pending.Metadata}.");

                PendingStartupMods.Clear();
            }

            private static void AddDependencyEdges(PendingStartupMod target, IEnumerable<EverestModuleMetadata> dependencies,
                bool optional, Dictionary<string, PendingStartupMod> candidates, HashSet<PendingStartupMod> valid,
                Dictionary<PendingStartupMod, List<(PendingStartupMod Target, bool Optional)>> outgoing,
                Dictionary<PendingStartupMod, int> indegree) {
                foreach (EverestModuleMetadata dependency in dependencies) {
                    if (DependencyLoaded(dependency))
                        continue;
                    if (candidates.TryGetValue(NormalizeDependencyName(dependency.Name), out PendingStartupMod source)
                        && valid.Contains(source)
                        && VersionSatisfiesDependency(dependency.Version, source.Metadata.Version)) {
                        outgoing[source].Add((target, optional));
                        indegree[target]++;
                    }
                }
            }

            private static void AssignStableContentCommitIndices(HashSet<PendingStartupMod> valid,
                Dictionary<PendingStartupMod, List<(PendingStartupMod Target, bool Optional)>> outgoing,
                Dictionary<PendingStartupMod, int> indegree) {
                Dictionary<PendingStartupMod, int> degree = indegree.ToDictionary(pair => pair.Key, pair => pair.Value);
                HashSet<PendingStartupMod> remaining = new HashSet<PendingStartupMod>(valid);
                List<PendingStartupMod> order = new List<PendingStartupMod>(valid.Count);
                PriorityQueue<PendingStartupMod, int> queue = new PriorityQueue<PendingStartupMod, int>();

                foreach (PendingStartupMod node in remaining)
                    if (degree[node] == 0)
                        queue.Enqueue(node, node.DiscoveryIndex);

                void Drain(bool ignoreOptionalEdges) {
                    while (queue.Count > 0) {
                        PendingStartupMod node = queue.Dequeue();
                        if (!remaining.Remove(node))
                            continue;
                        order.Add(node);
                        foreach ((PendingStartupMod target, bool optional) in outgoing[node]) {
                            if (!remaining.Contains(target) || ignoreOptionalEdges && optional)
                                continue;
                            if (--degree[target] == 0)
                                queue.Enqueue(target, target.DiscoveryIndex);
                        }
                    }
                }

                Drain(ignoreOptionalEdges: false);
                if (remaining.Count > 0) {
                    foreach (PendingStartupMod node in remaining)
                        degree[node] = 0;
                    foreach (PendingStartupMod source in remaining)
                        foreach ((PendingStartupMod target, bool optional) in outgoing[source])
                            if (!optional && remaining.Contains(target))
                                degree[target]++;
                    foreach (PendingStartupMod node in remaining)
                        if (degree[node] == 0)
                            queue.Enqueue(node, node.DiscoveryIndex);
                    Drain(ignoreOptionalEdges: true);
                }

                for (int index = 0; index < order.Count; index++)
                    order[index].ContentCommitIndex = index;
            }

            private static void AssignProfileGuidedSchedulingPriorities(HashSet<PendingStartupMod> valid,
                Dictionary<PendingStartupMod, List<(PendingStartupMod Target, bool Optional)>> outgoing) {
                foreach (PendingStartupMod node in valid)
                    node.EstimatedLoadMilliseconds = LoaderTimingCache.GetEstimate(node.Metadata, out node.TimingSamples);

                foreach (PendingStartupMod node in valid) {
                    // Keep the reserved lane as a hedge for older timing caches and for
                    // configurations which disable full PGO list scheduling.
                    node.PrefersReservedWorker = LoaderTimingCache.Enabled
                        && node.TimingSamples > 0
                        && node.EstimatedLoadMilliseconds >= 500d;
                    node.RequiresExclusiveExecution = RequiresExclusiveStartupExecution(node.Metadata);
                }

                // Bottom-level / critical-path weight: own historical cost plus the most
                // expensive reachable successor chain. This is a standard list-scheduling
                // heuristic and makes a slow dependency start before short independent leaves.
                foreach (PendingStartupMod node in valid
                    .Where(node => node.ContentCommitIndex >= 0)
                    .OrderByDescending(node => node.ContentCommitIndex)) {
                    double successor = outgoing[node]
                        .Where(edge => edge.Target.ContentCommitIndex > node.ContentCommitIndex)
                        .Select(edge => edge.Target.CriticalPathMilliseconds)
                        .DefaultIfEmpty(0d)
                        .Max();
                    node.CriticalPathMilliseconds = node.EstimatedLoadMilliseconds + successor;
                }

                // Container content and ILHook application retain their stable topological
                // commit/order indices. Module Load() bodies are already globally concurrent,
                // so start the longest dependency-ready critical paths first instead of making
                // expensive helpers wait behind dozens of short alphabetical leaves.
                IEnumerable<PendingStartupMod> schedulingOrder = valid
                    .Where(node => node.ContentCommitIndex >= 0);
                schedulingOrder = ProfileGuidedReorderingEnabled
                    ? schedulingOrder
                        .OrderByDescending(node => node.CriticalPathMilliseconds)
                        .ThenBy(node => node.ContentCommitIndex)
                    : schedulingOrder.OrderBy(node => node.ContentCommitIndex);
                int schedulingPriority = 0;
                foreach (PendingStartupMod node in schedulingOrder)
                    node.SchedulingPriority = schedulingPriority++;

                foreach (PendingStartupMod node in valid
                    .Where(node => node.ContentCommitIndex >= 0)
                    .OrderByDescending(node => node.CriticalPathMilliseconds)
                    .Take(8))
                    Logger.Verbose("loader", $"PGO candidate {node.Metadata.Name}: estimate={node.EstimatedLoadMilliseconds:F1}ms, samples={node.TimingSamples}, critical={node.CriticalPathMilliseconds:F1}ms, reserved={node.PrefersReservedWorker}");
            }

            private static void CommitContainerContentInStableOrder(HashSet<PendingStartupMod> valid) {
                // Container assets define conflict/override precedence. Commit them once in
                // stable topological order before any third-party module code runs. This
                // avoids making worker tasks block on a content ticket while retaining
                // deterministic asset precedence.
                foreach (PendingStartupMod pending in valid
                    .Where(node => node.ContentCommitIndex >= 0)
                    .OrderBy(node => node.ContentCommitIndex)) {
                    lock (ContentCrawlLock)
                        pending.ContentCrawl?.Invoke();
                }
            }

            private static void DrainReadyQueue(PriorityQueue<PendingStartupMod, int> ready, HashSet<PendingStartupMod> loaded,
                Dictionary<PendingStartupMod, List<(PendingStartupMod Target, bool Optional)>> outgoing,
                Dictionary<PendingStartupMod, int> indegree, bool ignoreOptionalEdges = false) {
                int degree = ParallelStartupEnabled ? ParallelStartupDegree : 1;
                Dictionary<Task<StartupLoadResult>, PendingStartupMod> running = new Dictionary<Task<StartupLoadResult>, PendingStartupMod>();
                List<Exception> errors = new List<Exception>();
                bool exclusiveTaskRunning = false;
                Task<StartupLoadResult> reservedWorkerTask = null;
                int regularTasksRunning = 0;

                void Complete(StartupLoadResult result) {
                    PendingStartupMod pending = result.Pending;
                    if (result.Error != null) {
                        errors.Add(new Exception($"Failed loading startup mod {pending.Metadata}", result.Error));
                        return;
                    }

                    if (result.Succeeded)
                        EverestSplashHandler.IncreaseLoadedModCount(pending.Metadata.Name);

                    foreach ((PendingStartupMod target, bool optional) in outgoing[pending]) {
                        if (ignoreOptionalEdges && optional)
                            continue;
                        if (--indegree[target] == 0)
                            ready.Enqueue(target, target.SchedulingPriority);
                        if (indegree[target] == 0)
                            LoaderProfiler.Mark($"startup-node-ready/{target.Metadata.Name}");
                    }
                }

                StartupLoadResult Run(PendingStartupMod pending) {
                    try {
                        return new StartupLoadResult {
                            Pending = pending,
                            Succeeded = LoadStartupNode(pending)
                        };
                    } catch (Exception e) {
                        return new StartupLoadResult {
                            Pending = pending,
                            Error = e
                        };
                    }
                }

                using (LoaderProfiler.Measure("dependency-dag-load")) {
                    while ((ready.Count > 0 && errors.Count == 0) || running.Count > 0) {
                        while (errors.Count == 0 && ready.Count > 0) {
                            if (exclusiveTaskRunning)
                                break;

                            PendingStartupMod pending = null;
                            bool useReservedWorker = false;

                            // The dedicated lane may look ahead among dependency-ready
                            // nodes. Regular workers still consume the stable topological
                            // queue, so PGO cannot reshuffle the whole startup as the first
                            // prototype did. This is list scheduling with one bounded
                            // speculative slot rather than a global priority rewrite.
                            if (degree > 1 && reservedWorkerTask == null && !ready.Peek().RequiresExclusiveExecution) {
                                pending = DequeueReservedWorkerCandidate(ready);
                                useReservedWorker = pending != null;
                            }

                            pending ??= ready.Peek();
                            if (pending.RequiresExclusiveExecution && running.Count > 0)
                                break;
                            if (!useReservedWorker && regularTasksRunning >= degree)
                                break;

                            if (!useReservedWorker)
                                ready.Dequeue();
                            if (!loaded.Add(pending))
                                continue;

                            if (degree == 1) {
                                Complete(Run(pending));
                            } else {
                                Task<StartupLoadResult> task = useReservedWorker
                                    ? Task.Factory.StartNew(
                                        () => Run(pending),
                                        CancellationToken.None,
                                        TaskCreationOptions.LongRunning,
                                        TaskScheduler.Default)
                                    : Task.Run(() => Run(pending));
                                running.Add(task, pending);
                                if (useReservedWorker) {
                                    reservedWorkerTask = task;
                                    Logger.Verbose("loader", $"PGO reserved worker assigned to {pending.Metadata.Name} (estimate {pending.EstimatedLoadMilliseconds:F1}ms).");
                                } else {
                                    regularTasksRunning++;
                                }
                                if (pending.RequiresExclusiveExecution) {
                                    exclusiveTaskRunning = true;
                                    Logger.Verbose("loader", $"Running compatibility-sensitive module {pending.Metadata.Name} exclusively.");
                                    break;
                                }
                            }
                        }

                        if (running.Count == 0)
                            continue;

                        Task.WhenAny(running.Keys).GetAwaiter().GetResult();
                        // Drain every task which completed in the same scheduling quantum in
                        // stable discovery order. A dependency becomes runnable immediately;
                        // there is no dependency-wave barrier.
                        Task<StartupLoadResult>[] completed = running.Keys
                            .Where(task => task.IsCompleted)
                            .OrderBy(task => running[task].ContentCommitIndex)
                            .ToArray();
                        foreach (Task<StartupLoadResult> task in completed) {
                            if (running[task].RequiresExclusiveExecution)
                                exclusiveTaskRunning = false;
                            if (task == reservedWorkerTask)
                                reservedWorkerTask = null;
                            else
                                regularTasksRunning--;
                            running.Remove(task);
                            Complete(task.GetAwaiter().GetResult());
                        }
                    }
                }

                if (errors.Count > 0)
                    throw new AggregateException(errors);
            }

            private static PendingStartupMod DequeueReservedWorkerCandidate(PriorityQueue<PendingStartupMod, int> ready) {
                PendingStartupMod candidate = ready.UnorderedItems
                    .Select(item => item.Element)
                    .Where(node => node.PrefersReservedWorker && !node.RequiresExclusiveExecution)
                    .OrderByDescending(node => node.CriticalPathMilliseconds)
                    .ThenBy(node => node.ContentCommitIndex)
                    .FirstOrDefault();
                if (candidate == null)
                    return null;

                // PriorityQueue has no arbitrary removal API. Rebuilding this tiny ready
                // frontier is inexpensive (well below the cost of a single assembly load)
                // and keeps all remaining priorities unchanged.
                List<(PendingStartupMod Node, int Priority)> retained = new List<(PendingStartupMod, int)>(ready.Count - 1);
                while (ready.TryDequeue(out PendingStartupMod node, out int priority))
                    if (!ReferenceEquals(node, candidate))
                        retained.Add((node, priority));
                foreach ((PendingStartupMod node, int priority) in retained)
                    ready.Enqueue(node, priority);
                return candidate;
            }

            private static bool LoadStartupNode(PendingStartupMod pending) {
                Stopwatch watch = Stopwatch.StartNew();
                string name = pending.Metadata?.Name ?? "unknown";
                LoaderProfiler.Mark($"startup-node-start/{name}");
                try {
                    using var ilHookOrder = MonoMod.RuntimeDetour.ILHookTransaction.EnterOrder(pending.ContentCommitIndex);
                    using var profileNode = LoaderProfiler.Measure("startup-node", name);
                    if (pending.Metadata.Dependencies.Any(dependency => !DependencyLoaded(dependency))) {
                        Logger.Warn("loader", $"A dependency failed while loading mod {pending.Metadata}; skipping it.");
                        return false;
                    }

                    return LoadMod(pending.Metadata);
                } finally {
                    watch.Stop();
                    LoaderTimingCache.Record(pending.Metadata, watch.Elapsed.TotalMilliseconds);
                    LoaderProfiler.Mark($"startup-node-end/{name}");
                }
            }

            private static string NormalizeDependencyName(string name) =>
                name == CoreModule.NETCoreMetaName ? CoreModule.Instance.Metadata.Name : name;

            /// <summary>
            /// Load a mod .dll given its metadata at runtime. Doesn't load the mod content.
            /// </summary>
            /// <param name="meta">Metadata of the mod to load.</param>
            /// <returns>Whether the mod load was successful.</returns>
            public static bool LoadMod(EverestModuleMetadata meta) {
                if (meta == null)
                    return true;

                using var _ = new ScopeFinalizer(() => {
                    lock (ModuleRegistrationFinalizeLock)
                        Events.Everest.LoadMod(meta);
                });

                using var profileLoadMod = LoaderProfiler.Measure("load-module");

                // Create an assembly context
                meta.AssemblyContext ??= new EverestModuleAssemblyContext(meta);

                // Try to load a module from a DLL
                if (!string.IsNullOrEmpty(meta.DLL)) {
                    Assembly asm;
                    using (LoaderProfiler.Measure("assembly-load"))
                    using (LoaderProfiler.Measure("assembly-load", meta.Name))
                        asm = meta.AssemblyContext.LoadAssemblyFromModPath(meta.DLL);
                    if (asm is null) {
                        // Don't register a module - this will cause dependencies to not load
                        Logger.Error("loader", $"Could not load DLL {meta.DLL} for mod {meta.Name}");
                        lock (ModsWithAssemblyLoadFailures)
                            ModsWithAssemblyLoadFailures.Add(meta);
                        return false;
                    }

                    LoadModAssembly(meta, asm);
                    goto success;
                }

                // Register a null module for content mods.
                new NullModule(meta).Register();
                success:
                meta.RegisterMod();
                return true;
            }

            /// <summary>
            /// Find and load all EverestModules in the given assembly.
            /// </summary>
            /// <param name="meta">The mod metadata, preferably from the mod metadata.yaml file.</param>
            /// <param name="asm">The mod assembly, preferably relinked.</param>
            public static void LoadModAssembly(EverestModuleMetadata meta, Assembly asm) {
                // Apply hackfixes
                ApplyModHackfixes(meta, asm);

                // Crawl assembly manifest content. During parallel startup the assets are
                // registered here (the global asset map is lock-protected), but the mod is
                // NOT appended to Content.Mods until a quiescent post-load point: appending
                // from a worker would race with another module's Load() enumerating
                // Content.Mods.
                ModContent content = new AssemblyModContent(asm) {
                    Mod = meta,
                    Name = meta.Name
                };
                using (LoaderProfiler.Measure("assembly-content-crawl"))
                lock (ContentCrawlLock)
                    Content.Crawl(content, registerInMods: !IsBatchLoading);
                if (IsBatchLoading)
                    meta.AssemblyContent = content;

                // Find and register all EverestModule subtypes in the assembly
                Type[] types;
                try {
                    using (LoaderProfiler.Measure("assembly-get-types"))
                    using (LoaderProfiler.Measure("assembly-get-types", meta.Name))
                        types = asm.GetTypesSafe();
                } catch (Exception e) {
                    Logger.Warn("loader", $"Failed reading assembly: {e}");
                    Logger.LogDetailed(e);
                    return;
                }

                bool foundModule = false;
                using (LoaderProfiler.Measure("module-discovery-register"))
                foreach (Type type in types) {
                    EverestModule mod = null;
                    try {
                        if (typeof(EverestModule).IsAssignableFrom(type) && !type.IsAbstract) {
                            foundModule = true;
                            if (!typeof(NullModule).IsAssignableFrom(type)) {
                                using (LoaderProfiler.Measure("module-constructor"))
                                    mod = (EverestModule) type.GetConstructor(Type.EmptyTypes).Invoke(null);
                            }
                        }
                    } catch (TypeLoadException e) {
                        // The type likely depends on a base class from a missing optional dependency
                        Logger.Warn("loader", $"Skipping type '{type.FullName}' likely depending on optional dependency: {e}");
                    }

                    if (mod != null) {
                        mod.Metadata = meta;
                        mod.Register();
                    }
                }

                // Warn if we didn't find a module, as that could indicate an oversight from the developer
                if (!foundModule)
                    Logger.Warn("loader", "Assembly doesn't contain an EverestModule!");

                using (LoaderProfiler.Measure("assembly-attribute-scan"))
                lock (AssemblyProcessingLock)
                    ProcessAssembly(meta, asm, types);
            }

            internal static void ProcessAssembly(EverestModuleMetadata meta, Assembly asm, Type[] types) {
                LuaLoader.Precache(asm);

                bool newStrawberriesRegistered = false;

                foreach (Type type in types) {
                    // Search for all entities marked with the CustomEntityAttribute.
                    foreach (CustomEntityAttribute attrib in type.GetCustomAttributes<CustomEntityAttribute>()) {
                        foreach (string idFull in attrib.IDs) {
                            string id;
                            string genName;
                            string[] split = idFull.Split('=');

                            if (split.Length == 1) {
                                id = split[0];
                                genName = "Load";

                            } else if (split.Length == 2) {
                                id = split[0];
                                genName = split[1];

                            } else {
                                Logger.Warn("core", $"Invalid number of custom entity ID elements: {idFull} ({type.FullName})");
                                continue;
                            }

                            id = id.Trim();
                            genName = genName.Trim();

                            patch_Level.EntityLoader loader = null;

                            ConstructorInfo ctor = null;
                            MethodInfo gen;

                            gen = type.GetMethod(genName, new Type[] { typeof(Level), typeof(LevelData), typeof(Vector2), typeof(EntityData) });
                            if (gen != null && gen.IsStatic && gen.ReturnType.IsCompatible(typeof(Entity))) {
                                loader = (level, levelData, offset, entityData) => {
                                    var entityId = ((patch_Level)level).CreateEntityId(levelData, entityData);
                                    var entity = (patch_Entity) gen.Invoke(null, new object[] { level, levelData, offset, entityData });
                                    if (entity != null) {
                                        entity.SourceData = entityData;
                                        entity.SourceId = entityId;
                                    }
                                    
                                    return entity;
                                };
                                goto RegisterEntityLoader;
                            }

                            ctor = type.GetConstructor(new Type[] { typeof(EntityData), typeof(Vector2), typeof(EntityID) });
                            if (ctor != null) {
                                loader = (level, levelData, offset, entityData) => {
                                    var entityId = ((patch_Level)level).CreateEntityId(levelData, entityData);
                                    var entity = (patch_Entity) ctor.Invoke(new object[] { entityData, offset, entityId });
                                    entity.SourceData = entityData;
                                    entity.SourceId = entityId;
                                    
                                    return entity;
                                };
                                goto RegisterEntityLoader;
                            }

                            ctor = type.GetConstructor(new Type[] { typeof(EntityData), typeof(Vector2) });
                            if (ctor != null) {
                                loader = (level, levelData, offset, entityData) => {
                                    var entity = (patch_Entity)ctor.Invoke(new object[] { entityData, offset });
                                    entity.SourceData = entityData;
                                    entity.SourceId = ((patch_Level)level).CreateEntityId(levelData, entityData);
                                    
                                    return entity;
                                };
                                goto RegisterEntityLoader;
                            }

                            ctor = type.GetConstructor(new Type[] { typeof(Vector2) });
                            if (ctor != null) {
                                loader = (level, levelData, offset, entityData) => {
                                    var entity = (patch_Entity)ctor.Invoke(new object[] { offset });
                                    entity.SourceData = entityData;
                                    entity.SourceId = ((patch_Level)level).CreateEntityId(levelData, entityData);
                                    return entity;
                                };
                                goto RegisterEntityLoader;
                            }

                            ctor = type.GetConstructor(Type.EmptyTypes);
                            if (ctor != null) {
                                loader = (level, levelData, offset, entityData) => {
                                    var entity = (patch_Entity)ctor.Invoke(null);
                                    entity.SourceData = entityData;
                                    entity.SourceId = ((patch_Level)level).CreateEntityId(levelData, entityData);
                                    return entity;
                                };
                                goto RegisterEntityLoader;
                            }

                            RegisterEntityLoader:
                            if (loader == null) {
                                Logger.Warn("core", $"Found custom entity without suitable constructor / {genName}(Level, LevelData, Vector2, EntityData): {id} ({type.FullName})");
                                continue;
                            }

                            // Immediately register the connection when we're calling the ctor,
                            // since we know the return type upfront.
                            if (ctor != null) {
                                EntityRegistry.RegisterSidToTypeConnection(id, ctor.DeclaringType);
                            }
                            
                            patch_Level.EntityLoaders[id] = loader;
                        }
                    }
                    // Register with the StrawberryRegistry all entities marked with RegisterStrawberryAttribute.
                    foreach (RegisterStrawberryAttribute attrib in type.GetCustomAttributes<RegisterStrawberryAttribute>()) {
                        List<string> names = new List<string>();
                        foreach (CustomEntityAttribute nameAttrib in type.GetCustomAttributes<CustomEntityAttribute>())
                            foreach (string idFull in nameAttrib.IDs) {
                                string[] split = idFull.Split('=');
                                if (split.Length == 0) {
                                    Logger.Warn("core", $"Invalid number of custom entity ID elements: {idFull} ({type.FullName})");
                                    continue;
                                }
                                names.Add(split[0]);
                            }
                        if (names.Count == 0)
                            goto NoDefinedBerryNames; // no customnames? skip out on registering berry

                        foreach (string name in names) {
                            StrawberryRegistry.Register(type, name, attrib.isTracked, attrib.blocksNormalCollection);
                            newStrawberriesRegistered = true;
                        }
                    }
                    NoDefinedBerryNames:
                    ;

                    // Search for all Entities marked with the CustomEventAttribute.
                    foreach (CustomEventAttribute attrib in type.GetCustomAttributes<CustomEventAttribute>()) {
                        foreach (string idFull in attrib.IDs) {
                            string id;
                            string genName;
                            string[] split = idFull.Split('=');

                            if (split.Length == 1) {
                                id = split[0];
                                genName = "Load";

                            } else if (split.Length == 2) {
                                id = split[0];
                                genName = split[1];

                            } else {
                                Logger.Warn("core", $"Invalid number of custom cutscene ID elements: {idFull} ({type.FullName})");
                                continue;
                            }

                            id = id.Trim();
                            genName = genName.Trim();

                            patch_EventTrigger.CutsceneLoader loader = null;

                            ConstructorInfo ctor;
                            MethodInfo gen;

                            gen = type.GetMethod(genName, new Type[] { typeof(EventTrigger), typeof(Player), typeof(string) });
                            if (gen != null && gen.IsStatic && gen.ReturnType.IsCompatible(typeof(Entity))) {
                                loader = (trigger, player, eventID) => (Entity) gen.Invoke(null, new object[] { trigger, player, eventID });
                                goto RegisterCutsceneLoader;
                            }

                            ctor = type.GetConstructor(new Type[] { typeof(EventTrigger), typeof(Player), typeof(string) });
                            if (ctor != null) {
                                loader = (trigger, player, eventID) => (Entity) ctor.Invoke(new object[] { trigger, player, eventID });
                                goto RegisterCutsceneLoader;
                            }

                            ctor = type.GetConstructor(Type.EmptyTypes);
                            if (ctor != null) {
                                loader = (trigger, player, eventID) => (Entity) ctor.Invoke(null);
                                goto RegisterCutsceneLoader;
                            }

                            RegisterCutsceneLoader:
                            if (loader == null) {
                                Logger.Warn("core", $"Found custom cutscene without suitable constructor / {genName}(EventTrigger, Player, string): {id} ({type.FullName})");
                                continue;
                            }
                            patch_EventTrigger.CutsceneLoaders[id] = loader;
                        }
                    }

                    // Search for all Backdrops marked with the CustomBackdropAttribute.
                    foreach (CustomBackdropAttribute attrib in type.GetCustomAttributes<CustomBackdropAttribute>()) {
                        foreach (string idFull in attrib.IDs) {
                            string id;
                            string genName;
                            string[] split = idFull.Split('=');

                            if (split.Length == 1) {
                                id = split[0];
                                genName = "Load";
                            } else if (split.Length == 2) {
                                id = split[0];
                                genName = split[1];
                            } else {
                                Logger.Warn("core", $"Invalid number of custom backdrop ID elements: {idFull} ({type.FullName})");
                                continue;
                            }

                            id = id.Trim();
                            genName = genName.Trim();

                            patch_MapData.BackdropLoader loader = null;

                            ConstructorInfo ctor;
                            MethodInfo gen;

                            gen = type.GetMethod(genName, new Type[] { typeof(BinaryPacker.Element) });
                            if (gen != null && gen.IsStatic && gen.ReturnType.IsCompatible(typeof(Backdrop))) {
                                loader = data => (Backdrop) gen.Invoke(null, new object[] { data });
                                goto RegisterBackdropLoader;
                            }

                            ctor = type.GetConstructor(new Type[] { typeof(BinaryPacker.Element) });
                            if (ctor != null) {
                                loader = data => (Backdrop) ctor.Invoke(new object[] { data });
                                goto RegisterBackdropLoader;
                            }

                            RegisterBackdropLoader:
                            if (loader == null) {
                                Logger.Warn("core", $"Found custom backdrop without suitable constructor / {genName}(BinaryPacker.Element): {id} ({type.FullName})");
                                continue;
                            }
                            patch_MapData.BackdropLoaders[id] = loader;
                        }
                    }

                    // we already are in the overworld. Register new Ouis real quick!
                    if (Engine.Instance != null && Engine.Scene is Overworld overworld && typeof(Oui).IsAssignableFrom(type) && !type.IsAbstract) {
                        Logger.Verbose("core", $"Instantiating UI from {meta}: {type.FullName}");
                        ((patch_Overworld) overworld).RegisterOui(type);
                    }
                }
                // We should run the map data processors again if new berry types are registered, so that CoreMapDataProcessor assigns them checkpoint IDs and orders.
                if (newStrawberriesRegistered && _Initialized) {
                    Logger.Verbose("core", $"Assembly {asm.FullName} for module {meta} has custom strawberries: triggering map reload.");
                    TriggerModInitMapReload();
                }
            }

            /// <summary>
            /// Reload a mod .dll and all mods depending on it given its metadata at runtime. Doesn't reload the mod content.
            /// </summary>
            /// <param name="meta">Metadata of the mod to reload.</param>
            public static void ReloadMod(EverestModuleMetadata meta) {
                if (meta.AssemblyContext == null)
                    return;

                QueuedTaskHelperV2.Do($"ReloadModAssembly: {meta.Name}", () => {
                    Logger.Info("loader", $"Reloading mod assemblies: {meta.Name}");

                    AssetReloadHelper.Do($"{Dialog.Clean("ASSETRELOADHELPER_RELOADINGMODASSEMBLY")} {meta.Name}", () => {
                        // Determine the order to load/unload modules in
                        List<EverestModuleMetadata> reloadMods = new List<EverestModuleMetadata>();
                        lock (Everest._Modules) {
                            // Create reverse dependency graph
                            Dictionary<string, List<EverestModule>> revDeps = new Dictionary<string, List<EverestModule>>();
                            Everest._Modules.ForEach(mod => revDeps.TryAdd(mod.Metadata.Name, new List<EverestModule>()));

                            foreach (EverestModule mod in Everest._Modules)
                                foreach (EverestModuleAssemblyContext depAsmCtx in mod.Metadata.AssemblyContext?.ActiveDependencyContexts ?? Enumerable.Empty<EverestModuleAssemblyContext>())
                                    revDeps.GetValueOrDefault(depAsmCtx.ModuleMeta.Name)?.Add(mod);

                            // Run a DFS over the reverse dependency graph to determine the reload order
                            HashSet<string> visited = new HashSet<string>();
                            void VisitMod(EverestModuleMetadata node) {
                                // Check if we already visited this node
                                if (!visited.Add(node.Name))
                                    return;

                                // Ensure mods which depend on this one are placed before this mod in the reload order
                                revDeps[node.Name].ForEach(revDep => VisitMod(revDep.Metadata));
                                reloadMods.Add(node);
                            }
                            VisitMod(meta);
                        }

                        // Unload modules in the order determined before (dependents before dependencies)
                        foreach (EverestModuleMetadata unloadMod in reloadMods) {
                            Logger.Verbose("loader", $"-> unloading: {unloadMod.Name}");
                            unloadMod.AssemblyContext?.Dispose();
                            unloadMod.AssemblyContext = null;
                            unloadMod.InvalidateHash();
                        }

                        // Load modules in the reverse order determined before (dependencies before dependents)
                        // Delay initialization until all mods have been loaded
                        using (new ModInitializationBatch()) {
                            foreach (EverestModuleMetadata loadMod in reloadMods.Reverse<EverestModuleMetadata>()) {
                                if (loadMod.Dependencies.Any(dep => !DependencyLoaded(dep))) {
                                    Logger.Warn("loader", $"-> skipping reload of mod '{loadMod.Name}' as dependency failed to load");
                                    continue;
                                }

                                Logger.Verbose("loader", $"-> reloading: {loadMod.Name}");
                                if (!LoadMod(loadMod))
                                    Logger.Warn("loader", $"-> failed to reload mod '{loadMod.Name}'!");
                            }
                        }
                    }, static () => AssetReloadHelper.ReloadLevel(true));
                });
            }

            /// <summary>
            /// Checks if all dependencies are loaded.
            /// Can be used by mods manually to f.e. activate / disable functionality.
            /// </summary>
            /// <param name="meta">The metadata of the mod listing the dependencies.</param>
            /// <returns>True if the dependencies have already been loaded by Everest, false otherwise.</returns>
            public static bool DependenciesLoaded(EverestModuleMetadata meta) {
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

                // Harcode EverestCore as an alias for the core module
                if (depName == CoreModule.NETCoreMetaName)
                    depName = CoreModule.Instance.Metadata.Name;

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
