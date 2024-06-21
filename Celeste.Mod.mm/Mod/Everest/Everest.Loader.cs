using Celeste.Mod.Backdrops;
using Celeste.Mod.Core;
using Celeste.Mod.Entities;
using Celeste.Mod.Helpers;
using Ionic.Zip;
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

                string[] files = Directory
                    .GetFiles(PathMods)
                    .OrderBy(f => f) //Prevent inode loading jank
                    .Select(Path.GetFileName)
                    .Where(file => file.EndsWith(".zip") && ShouldLoadFile(file))
                    .ToArray();
                   
                string[] dirs = Directory
                    .GetDirectories(PathMods)
                    .OrderBy(f => f) //Prevent inode loading jank
                    .Select(Path.GetFileName)
                    .Where(file => file != "Cache" && ShouldLoadFile(file))
                    .ToArray();

                EverestSplashHandler.SetSplashLoadingModCount(files.Length + dirs.Length);

                foreach (string file in files) {
                    LoadZip(Path.Combine(PathMods, file));
                }
                foreach (string dir in dirs) {
                    LoadDir(Path.Combine(PathMods, dir));
                }

                enforceOptionalDependencies = false;
                Logger.Log(LogLevel.Info, "loader", "Loading mods with unsatisfied optional dependencies (if any)");
                Everest.CheckDependenciesOfDelayedMods();

                EverestSplashHandler.AllModsLoaded();

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
                QueuedTaskHelper.Do("LoadAutoUpdated:" + e.FullPath, () => AssetReloadHelper.Do($"{Dialog.Clean("ASSETRELOADHELPER_LOADINGNEWMOD")} {Path.GetFileName(e.FullPath)}", () => MainThreadHelper.Schedule(() => {
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
                if (!File.Exists(archive)) { // It just doesn't exist.
                    EverestSplashHandler.IncreaseLoadedModCount(null); // Increase the splash count anyway, since it was detected as an entry
                    return;
                }

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
                if (!Flags.SupportRuntimeMods) {
                    Logger.Log(LogLevel.Warn, "loader", "Loader disabled!");
                    return;
                }

                if (!Directory.Exists(dir)) // Relative path?
                    dir = Path.Combine(PathMods, dir);
                if (!Directory.Exists(dir)) { // It just doesn't exist.
                    EverestSplashHandler.IncreaseLoadedModCount(null); // Increase the splash count anyway, since it was detected as an entry
                    return;
                }

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

                EverestSplashHandler.IncreaseLoadedModCount(meta.Name);
                LoadMod(meta);
            }

            /// <summary>
            /// Load a mod .dll given its metadata at runtime. Doesn't load the mod content.
            /// </summary>
            /// <param name="meta">Metadata of the mod to load.</param>
            /// <returns>Whether the mod load was successful.</returns>
            public static bool LoadMod(EverestModuleMetadata meta) {
                if (!Flags.SupportRuntimeMods) {
                    Logger.Log(LogLevel.Warn, "loader", "Loader disabled!");
                    return false;
                }

                if (meta == null)
                    return true;

                using var _ = new ScopeFinalizer(() => Events.Everest.LoadMod(meta));

                // Create an assembly context
                meta.AssemblyContext ??= new EverestModuleAssemblyContext(meta);

                // Try to load a module from a DLL
                if (!string.IsNullOrEmpty(meta.DLL)) {
                    if (meta.AssemblyContext.LoadAssemblyFromModPath(meta.DLL) is not Assembly asm) {
                        // Don't register a module - this will cause dependencies to not load
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
                if (!Flags.SupportRuntimeMods) {
                    Logger.Log(LogLevel.Warn, "loader", "Loader disabled!");
                    return;
                }

                // Apply hackfixes
                ApplyModHackfixes(meta, asm);

                // Crawl assembly manifest content
                Content.Crawl(new AssemblyModContent(asm) {
                    Mod = meta,
                    Name = meta.Name
                });

                // Find and register all EverestModule subtypes in the assembly
                Type[] types;
                try {
                    types = asm.GetTypesSafe();
                } catch (Exception e) {
                    Logger.Log(LogLevel.Warn, "loader", $"Failed reading assembly: {e}");
                    e.LogDetailed();
                    return;
                }

                bool foundModule = false;
                foreach (Type type in types) {
                    EverestModule mod = null;
                    try {
                        if (typeof(EverestModule).IsAssignableFrom(type) && !type.IsAbstract) {
                            foundModule = true;
                            if (!typeof(NullModule).IsAssignableFrom(type)) {
                                mod = (EverestModule) type.GetConstructor(Type.EmptyTypes).Invoke(null);
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
                                Logger.Log(LogLevel.Warn, "core", $"Invalid number of custom entity ID elements: {idFull} ({type.FullName})");
                                continue;
                            }

                            id = id.Trim();
                            genName = genName.Trim();

                            patch_Level.EntityLoader loader = null;

                            ConstructorInfo ctor;
                            MethodInfo gen;

                            gen = type.GetMethod(genName, new Type[] { typeof(Level), typeof(LevelData), typeof(Vector2), typeof(EntityData) });
                            if (gen != null && gen.IsStatic && gen.ReturnType.IsCompatible(typeof(Entity))) {
                                loader = (level, levelData, offset, entityData) => (Entity) gen.Invoke(null, new object[] { level, levelData, offset, entityData });
                                goto RegisterEntityLoader;
                            }

                            ctor = type.GetConstructor(new Type[] { typeof(EntityData), typeof(Vector2), typeof(EntityID) });
                            if (ctor != null) {
                                loader = (level, levelData, offset, entityData) => (Entity) ctor.Invoke(new object[] { entityData, offset, new EntityID(levelData.Name, entityData.ID + (patch_Level._isLoadingTriggers ? 10000000 : 0)) });
                                goto RegisterEntityLoader;
                            }

                            ctor = type.GetConstructor(new Type[] { typeof(EntityData), typeof(Vector2) });
                            if (ctor != null) {
                                loader = (level, levelData, offset, entityData) => (Entity) ctor.Invoke(new object[] { entityData, offset });
                                goto RegisterEntityLoader;
                            }

                            ctor = type.GetConstructor(new Type[] { typeof(Vector2) });
                            if (ctor != null) {
                                loader = (level, levelData, offset, entityData) => (Entity) ctor.Invoke(new object[] { offset });
                                goto RegisterEntityLoader;
                            }

                            ctor = type.GetConstructor(Type.EmptyTypes);
                            if (ctor != null) {
                                loader = (level, levelData, offset, entityData) => (Entity) ctor.Invoke(null);
                                goto RegisterEntityLoader;
                            }

                            RegisterEntityLoader:
                            if (loader == null) {
                                Logger.Log(LogLevel.Warn, "core", $"Found custom entity without suitable constructor / {genName}(Level, LevelData, Vector2, EntityData): {id} ({type.FullName})");
                                continue;
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
                                    Logger.Log(LogLevel.Warn, "core", $"Invalid number of custom entity ID elements: {idFull} ({type.FullName})");
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
                                Logger.Log(LogLevel.Warn, "core", $"Invalid number of custom cutscene ID elements: {idFull} ({type.FullName})");
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
                                Logger.Log(LogLevel.Warn, "core", $"Found custom cutscene without suitable constructor / {genName}(EventTrigger, Player, string): {id} ({type.FullName})");
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
                                Logger.Log(LogLevel.Warn, "core", $"Invalid number of custom backdrop ID elements: {idFull} ({type.FullName})");
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
                                Logger.Log(LogLevel.Warn, "core", $"Found custom backdrop without suitable constructor / {genName}(BinaryPacker.Element): {id} ({type.FullName})");
                                continue;
                            }
                            patch_MapData.BackdropLoaders[id] = loader;
                        }
                    }

                    // we already are in the overworld. Register new Ouis real quick!
                    if (Engine.Instance != null && Engine.Scene is Overworld overworld && typeof(Oui).IsAssignableFrom(type) && !type.IsAbstract) {
                        Logger.Log(LogLevel.Verbose, "core", $"Instantiating UI from {meta}: {type.FullName}");

                        Oui oui = (Oui) Activator.CreateInstance(type);
                        oui.Visible = false;
                        overworld.Add(oui);
                        overworld.UIs.Add(oui);
                    }
                }
                // We should run the map data processors again if new berry types are registered, so that CoreMapDataProcessor assigns them checkpoint IDs and orders.
                if (newStrawberriesRegistered && _Initialized) {
                    Logger.Log(LogLevel.Verbose, "core", $"Assembly {asm.FullName} for module {meta} has custom strawberries: triggering map reload.");
                    TriggerModInitMapReload();
                }
            }

            /// <summary>
            /// Reload a mod .dll and all mods depending on it given its metadata at runtime. Doesn't reload the mod content.
            /// </summary>
            /// <param name="meta">Metadata of the mod to reload.</param>
            public static void ReloadMod(EverestModuleMetadata meta) {
                if (!Flags.SupportRuntimeMods || meta.AssemblyContext == null)
                    return;

                QueuedTaskHelper.Do($"ReloadModAssembly: {meta.Name}", () => {
                    Logger.Log(LogLevel.Info, "loader", $"Reloading mod assemblies: {meta.Name}");

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
                            Logger.Log(LogLevel.Verbose, "loader", $"-> unloading: {unloadMod.Name}");
                            unloadMod.AssemblyContext?.Dispose();
                            unloadMod.AssemblyContext = null;
                        }

                        // Load modules in the reverse order determined before (dependencies before dependents)
                        // Delay initialization until all mods have been loaded
                        using (new ModInitializationBatch()) {
                            foreach (EverestModuleMetadata loadMod in reloadMods.Reverse<EverestModuleMetadata>()) {
                                if (loadMod.Dependencies.Any(dep => !DependencyLoaded(dep))) {
                                    Logger.Log(LogLevel.Warn, "loader", $"-> skipping reload of mod '{loadMod.Name}' as dependency failed to load");
                                    continue;
                                }

                                Logger.Log(LogLevel.Verbose, "loader", $"-> reloading: {loadMod.Name}");
                                if (!LoadMod(loadMod))
                                    Logger.Log(LogLevel.Warn, "loader", $"-> failed to reload mod '{loadMod.Name}'!");
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
