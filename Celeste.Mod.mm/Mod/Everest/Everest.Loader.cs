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
            public static ReadOnlyCollection<string> Blacklist => _Blacklist.AsReadOnly();

            internal static List<Tuple<EverestModuleMetadata, Action>> Delayed = new List<Tuple<EverestModuleMetadata, Action>>();
            internal static int DelayedLock;

            internal static void LoadAuto() {
                Directory.CreateDirectory(PathMods = Path.Combine(PathGame, "Mods"));
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

                string[] files = Directory.GetFiles(PathMods);
                for (int i = 0; i < files.Length; i++) {
                    string file = Path.GetFileName(files[i]);
                    if (!file.EndsWith(".zip") || _Blacklist.Contains(file))
                        continue;
                    LoadZip(file);
                }
                files = Directory.GetDirectories(PathMods);
                for (int i = 0; i < files.Length; i++) {
                    string file = Path.GetFileName(files[i]);
                    if (file == "Cache" || _Blacklist.Contains(file))
                        continue;
                    LoadDir(file);
                }

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

                ContentModMetadata contentMeta = new ContentModMetadata {
                    PathArchive = archive
                };

                Action contentCrawl = () => {
                    if (contentMeta == null)
                        return;
                    Content.Crawl(contentMeta);
                    contentMeta = null;
                };

                if (multimetas != null) {
                    foreach (EverestModuleMetadata multimeta in multimetas) {
                        LoadModDelayed(multimeta, contentCrawl);
                    }
                } else {
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
                            Logger.Log(LogLevel.Warn, "loader", $"Failed parsing multimetadata.yaml in {dir}: {e}");
                        }
                    }

                ContentModMetadata contentMeta = new ContentModMetadata {
                    PathDirectory = dir
                };

                Action contentCrawl = () => {
                    if (contentMeta == null)
                        return;
                    Content.Crawl(contentMeta);
                    contentMeta = null;
                };

                if (multimetas != null) {
                    foreach (EverestModuleMetadata multimeta in multimetas) {
                        LoadModDelayed(multimeta, contentCrawl);
                    }
                } else {
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

                foreach (EverestModuleMetadata dep in meta.Dependencies)
                    if (!DependencyLoaded(dep)) {
                        Logger.Log(LogLevel.Info, "loader", $"Dependency {dep} of mod {meta} not loaded! Delaying.");
                        lock (Delayed) {
                            Delayed.Add(Tuple.Create(meta, callback));
                        }
                        return;
                    }

                LoadMod(meta);

                callback?.Invoke();
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

                // Add an AssemblyResolve handler for all bundled libraries.
                AppDomain.CurrentDomain.AssemblyResolve += GenerateModAssemblyResolver(meta);

                if (string.IsNullOrEmpty(meta.DLL))
                    return;

                // Load the actual assembly.
                Assembly asm = null;
                if (!string.IsNullOrEmpty(meta.PathArchive)) {
                    using (ZipFile zip = new ZipFile(meta.PathArchive)) {
                        foreach (ZipEntry entry in zip.Entries) {
                            string entryName = entry.FileName.Replace('\\', '/');
                            if (entryName == meta.DLL) {
                                using (MemoryStream stream = entry.ExtractStream()) {
                                    if (meta.Prelinked) {
                                        asm = Assembly.Load(stream.GetBuffer());
                                    } else {
                                        asm = Relinker.GetRelinkedAssembly(meta, stream);
                                    }
                                }
                            }
                        }
                    }

                } else {
                    if (meta.Prelinked)
                        asm = Assembly.LoadFrom(meta.DLL);
                    else
                        using (FileStream stream = File.OpenRead(meta.DLL))
                            asm = Relinker.GetRelinkedAssembly(meta, stream);
                }

                if (asm != null)
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

                Content.Crawl(new ContentModMetadata {
                    Assembly = asm
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
                    if (!typeof(EverestModule).IsAssignableFrom(type) || type.IsAbstract)
                        continue;

                    EverestModule mod = (EverestModule) type.GetConstructor(_EmptyTypeArray).Invoke(_EmptyObjectArray);
                    mod.Metadata = meta;
                    mod.Register();
                }
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

                    // Special case: Always true if version == 0.0.*
                    if (version.Major == 0 && version.Minor == 0)
                        return true;
                    // Major version, breaking changes, must match.
                    if (version.Major != depVersion.Major)
                        return false;
                    // Minor version, non-breaking changes, installed can't be lower than what we depend on.
                    if (version.Minor < depVersion.Minor)
                        return false;
                    return true;
                }

                return false;
            }

            private static ResolveEventHandler GenerateModAssemblyResolver(EverestModuleMetadata meta) {
                if (!string.IsNullOrEmpty(meta.PathArchive)) {
                    return (sender, args) => {
                        string asmName = new AssemblyName(args.Name).Name + ".dll";
                        using (ZipFile zip = new ZipFile(meta.PathArchive)) {
                            foreach (ZipEntry entry in zip.Entries) {
                                if (entry.FileName != asmName)
                                    continue;
                                using (MemoryStream stream = entry.ExtractStream()) {
                                    return Assembly.Load(stream.GetBuffer());
                                }
                            }
                        }
                        return null;
                    };
                }

                if (!string.IsNullOrEmpty(meta.PathDirectory)) {
                    return (sender, args) => {
                        string asmPath = Path.Combine(meta.PathDirectory, new AssemblyName(args.Name).Name + ".dll");
                        if (!File.Exists(asmPath))
                            return null;
                        return Assembly.LoadFrom(asmPath);
                    };
                }

                return null;
            }

        }
    }
}
