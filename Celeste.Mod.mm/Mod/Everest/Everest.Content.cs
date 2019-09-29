using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Monocle;
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
using Celeste.Mod.Core;
using System.Threading;

namespace Celeste.Mod {
    // Special meta types.
    public sealed class AssetTypeDirectory { private AssetTypeDirectory() { } }
    public sealed class AssetTypeAssembly { private AssetTypeAssembly() { } }
    public sealed class AssetTypeYaml { private AssetTypeYaml() { } }
    public sealed class AssetTypeXml { private AssetTypeXml() { } }
    public sealed class AssetTypeText { private AssetTypeText() { } }
    public sealed class AssetTypeLua { private AssetTypeLua() { } }
    public sealed class AssetTypeDialog { private AssetTypeDialog() { } }
    public sealed class AssetTypeDialogExport { private AssetTypeDialogExport() { } }
    public sealed class AssetTypeMap { private AssetTypeMap() { } }
    public sealed class AssetTypeTutorial { private AssetTypeTutorial() { } }
    public sealed class AssetTypeBank { private AssetTypeBank() { } }
    public sealed class AssetTypeGUIDs { private AssetTypeGUIDs() { } }

    // Delegate types.
    public delegate string TypeGuesser(string file, out Type type, out string format);

    // Source types.
    public abstract class ModContent : IDisposable {
        public virtual string DefaultName { get; }
        private string _Name;
        public string Name {
            get => !string.IsNullOrEmpty(_Name) ? _Name : DefaultName;
            set => _Name = value;
        }

        public EverestModuleMetadata Mod;

        public readonly List<ModAsset> List = new List<ModAsset>();
        public readonly Dictionary<string, ModAsset> Map = new Dictionary<string, ModAsset>();

        protected abstract void Crawl();
        internal void _Crawl() => Crawl();

        protected virtual void Add(string path, ModAsset asset) {
            Everest.Content.Add(path, asset);
            List.Add(asset);
            Map[asset.PathVirtual] = asset;
        }

        protected virtual void Update(string path, ModAsset next) {
            if (next != null)
                next.PathVirtual = path;

            if (Map.TryGetValue(path, out ModAsset prev)) {
                Update(prev, next);

            } else if (next != null) {
                Update((ModAsset) null, next);
            }
        }

        protected virtual void Update(ModAsset prev, ModAsset next) {
            if (prev != null) {
                int index = List.IndexOf(prev);

                if (next == null) {
                    Map.Remove(prev.PathVirtual);
                    if (index != -1)
                        List.RemoveAt(index);

                    Everest.Content.Update(prev, null);
                    foreach (ModAsset child in prev.Children.ToArray())
                        if (child.Source == this)
                            Update(child, null);

                } else {
                    Map[prev.PathVirtual] = next;
                    if (index != -1)
                        List[index] = next;
                    else
                        List.Add(next);

                    Everest.Content.Update(prev, next);
                    foreach (ModAsset child in prev.Children.ToArray())
                        if (child.Source == this)
                            Update(child, null);
                    foreach (ModAsset child in next.Children.ToArray())
                        if (child.Source == this)
                            Update((ModAsset) null, child);
                }

            } else if (next != null) {
                Map[next.PathVirtual] = next;
                List.Add(next);
                Everest.Content.Update(null, next);
                foreach (ModAsset child in next.Children.ToArray())
                    if (child.Source == this)
                        Update((ModAsset) null, child);
            }
        }

        private bool disposed = false;

        ~ModContent() {
            if (disposed)
                return;
            disposed = true;

            Dispose(false);
        }

        public void Dispose() {
            if (disposed)
                return;
            disposed = true;

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
        }
    }

    public class FileSystemModContent : ModContent {
        public override string DefaultName => System.IO.Path.GetFileName(Path);

        /// <summary>
        /// The path to the mod directory.
        /// </summary>
        public readonly string Path;

        private readonly Dictionary<string, FileSystemModAsset> FileSystemMap = new Dictionary<string, FileSystemModAsset>();

        private FileSystemWatcher watcher;

        public FileSystemModContent(string path) {
            Path = path;

            watcher = new FileSystemWatcher {
                Path = path,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                IncludeSubdirectories = true
            };

            watcher.Changed += FileUpdated;
            watcher.Created += FileUpdated;
            watcher.Deleted += FileUpdated;
            watcher.Renamed += FileRenamed;

            watcher.EnableRaisingEvents = true;
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            watcher.Dispose();
        }

        protected override void Crawl() => Crawl(null, Path, false);

        protected virtual void Crawl(string dir, string root, bool update) {
            if (dir == null)
                dir = Path;
            if (root == null)
                root = Path;

            if (File.Exists(dir)) {
                string path = dir.Substring(root.Length + 1);
                ModAsset asset = new FileSystemModAsset(this, dir);

                if (update)
                    Update(path, asset);
                else
                    Add(path, asset);
                return;
            }

            string[] files = Directory.GetFiles(dir);
            for (int i = 0; i < files.Length; i++) {
                string file = files[i];
                Crawl(file, root, update);
            }

            files = Directory.GetDirectories(dir);
            for (int i = 0; i < files.Length; i++) {
                string file = files[i];
                Crawl(file, root, update);
            }
        }

        protected override void Add(string path, ModAsset asset) {
            FileSystemModAsset fsma = (FileSystemModAsset) asset;
            FileSystemMap[fsma.Path] = fsma;
            base.Add(path, asset);
        }

        protected override void Update(string path, ModAsset next) {
            FileSystemModAsset fsma;
            if ((fsma = next as FileSystemModAsset) != null) {
                FileSystemMap[fsma.Path] = fsma;
            }
            base.Update(path, next);
        }

        protected override void Update(ModAsset prev, ModAsset next) {
            FileSystemModAsset fsma;
            if ((fsma = prev as FileSystemModAsset) != null) {
                FileSystemMap[fsma.Path] = null;
            }

            if ((fsma = next as FileSystemModAsset) != null) {
                FileSystemMap[fsma.Path] = fsma;
            }

            base.Update(prev, next);
        }

        private void FileUpdated(object source, FileSystemEventArgs e) {
            // Directories will be "changed" as soon as their children change.
            if (e.ChangeType == WatcherChangeTypes.Changed && Directory.Exists(e.FullPath))
                return;

            Logger.Log("content", $"File updated: {e.FullPath} - {e.ChangeType}");
            MainThreadHelper.Do(e.FullPath, () => Update(e.FullPath, e.FullPath));
        }

        private void FileRenamed(object source, RenamedEventArgs e) {
            Logger.Log("content", $"File renamed: {e.OldFullPath} - {e.FullPath}");
            MainThreadHelper.Do(Tuple.Create(e.OldFullPath, e.FullPath), () => Update(e.OldFullPath, e.FullPath));
        }

        private void Update(string pathPrev, string pathNext) {
            FileSystemModAsset prevFS;
            ModAsset prev = null;
            if (!FileSystemMap.TryGetValue(pathPrev, out prevFS) && !Everest.Content.TryGet<AssetTypeDirectory>(pathPrev.Substring(Path.Length + 1), out prev)) {
                prevFS = null;
            }

            prev = prevFS ?? prev;

            if (File.Exists(pathNext)) {
                Update(prev, new FileSystemModAsset(this, pathNext));

            } else if (Directory.Exists(pathNext)) {
                Update(prev, null);
                Crawl(pathNext, Path, true);

            } else {
                Update(prev, null);
            }
        }
    }

    public class MapBinsInModsModContent : ModContent {
        public override string DefaultName => System.IO.Path.GetFileName(Path);

        /// <summary>
        /// The path to the mod directory.
        /// </summary>
        public readonly string Path;

        public MapBinsInModsModContent(string path) {
            Path = path;
        }

        protected override void Crawl() {
            string[] files = Directory.GetFiles(Path);
            for (int i = 0; i < files.Length; i++) {
                string file = files[i];
                string name = file.Substring(Path.Length + 1);
                if (!file.EndsWith(".bin") || Everest.Loader._Blacklist.Contains(name))
                    continue;
                if (Everest.Loader._Whitelist != null && !Everest.Loader._Whitelist.Contains(name))
                    continue;
                Add("Maps/" + file.Substring(Path.Length + 1), new MapBinsInModsModAsset(this, file));
            }
        }
    }

    public class AssemblyModContent : ModContent {
        public override string DefaultName => Assembly.GetName().Name;

        /// <summary>
        /// The assembly containing the mod content as resources.
        /// </summary>
        public readonly Assembly Assembly;

        public AssemblyModContent(Assembly asm) {
            Assembly = asm;
        }

        protected override void Crawl() {
            string[] resourceNames = Assembly.GetManifestResourceNames();
            for (int i = 0; i < resourceNames.Length; i++) {
                string name = resourceNames[i];
                int indexOfContent = name.IndexOf("Content");
                if (indexOfContent < 0)
                    continue;
                name = name.Substring(indexOfContent + 8);
                Add(name, new AssemblyModAsset(this, resourceNames[i]));
            }
        }
    }

    public class ZipModContent : ModContent {
        public override string DefaultName => System.IO.Path.GetFileName(Path);

        /// <summary>
        /// The path to the archive containing the mod content.
        /// </summary>
        public readonly string Path;

        /// <summary>
        /// The loaded archive containing the mod content.
        /// </summary>
        public readonly ZipFile Zip;

        public ZipModContent(string path) {
            Path = path;
            Zip = new ZipFile(path);
        }

        protected override void Crawl() {
            foreach (ZipEntry entry in Zip.Entries) {
                string entryName = entry.FileName.Replace('\\', '/');
                if (entryName.EndsWith("/"))
                    continue;
                Add(entryName, new ZipModAsset(this, entry));
            }
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            Zip.Dispose();
        }
    }

    // Main helper type.
    public static partial class Everest {
        public static class Content {

            /// <summary>
            /// Whether or not Everest should dump all game assets into a user-friendly format on load (technically on Process).
            /// </summary>
            public static bool DumpOnLoad = false;
            internal static bool _DumpAll = false;

            /// <summary>
            /// The path to the original /Content directory.
            /// </summary>
            public static string PathContentOrig { get; internal set; }
            /// <summary>
            /// The path to the Everest /ModDUMP directory.
            /// </summary>
            public static string PathDUMP { get; internal set; }

            /// <summary>
            /// List of all currently loaded content mods.
            /// </summary>
            public readonly static List<ModContent> Mods = new List<ModContent>();

            /// <summary>
            /// Mod content mapping. Use Everest.Content.Add, Get, and TryGet where applicable instead.
            /// </summary>
            public readonly static Dictionary<string, ModAsset> Map = new Dictionary<string, ModAsset>();

            internal readonly static List<string> LoadedAssetPaths = new List<string>();
            internal readonly static List<string> LoadedAssetFullPaths = new List<string>();
            internal readonly static List<WeakReference> LoadedAssets = new List<WeakReference>();

            internal readonly static char[] DirSplit = { '/' };

            internal static void Initialize() {
                Celeste.Instance.Content = new EverestContentManager(Celeste.Instance.Content);

                Directory.CreateDirectory(PathContentOrig = Path.Combine(PathGame, Celeste.Instance.Content.RootDirectory));
                Directory.CreateDirectory(PathDUMP = Path.Combine(PathEverest, "ModDUMP"));

                if (_DumpAll)
                    DumpAll();

                if (Flags.IsDisabled)
                    return;

                Crawl(new AssemblyModContent(typeof(Everest).Assembly) {
                    Name = "Everest",
                    // Mod = CoreModule.Instance.Metadata // Can't actually set Mod this early.
                });
            }

            /// <summary>
            /// Gets the ModAsset mapped to the given relative path.
            /// </summary>
            /// <param name="path">The relative asset path.</param>
            /// <param name="metadata">The resulting mod asset meta object.</param>
            /// <param name="includeDirs">Whether to include directories or not.</param>
            /// <returns>True if a mapping for the given path is present, false otherwise.</returns>
            public static bool TryGet(string path, out ModAsset metadata, bool includeDirs = false) {
                path = path.Replace('\\', '/');

                if (Map.TryGetValue(path, out metadata) && metadata != null)
                    return true;

                metadata = null;
                return false;
            }
            /// <summary>
            /// Gets the ModAsset mapped to the given relative path.
            /// </summary>
            /// <param name="path">The relative asset path.</param>
            /// <param name="includeDirs">Whether to include directories or not.</param>
            /// <returns>The resulting mod asset meta object, or null.</returns>
            public static ModAsset Get(string path, bool includeDirs = false) {
                ModAsset metadata;
                if (TryGet(path, out metadata, includeDirs))
                    return metadata;
                return null;
            }

            /// <summary>
            /// Gets the ModAsset mapped to the given relative path.
            /// </summary>
            /// <param name="path">The relative asset path.</param>
            /// <param name="metadata">The resulting mod asset meta object.</param>
            /// <param name="includeDirs">Whether to include directories or not.</param>
            /// <returns>True if a mapping for the given path is present, false otherwise.</returns>
            public static bool TryGet<T>(string path, out ModAsset metadata, bool includeDirs = false) {
                path = path.Replace('\\', '/');

                List<string> parts = new List<string>(path.Split(DirSplit, StringSplitOptions.RemoveEmptyEntries));
                for (int i = 0; i < parts.Count; i++) {
                    string part = parts[i];

                    if (part == "..") {
                        parts.RemoveAt(i);
                        parts.RemoveAt(i - 1);
                        i -= 2;
                        continue;
                    }

                    if (part == ".") {
                        parts.RemoveAt(i);
                        i -= 1;
                        continue;
                    }
                }

                path = string.Join("/", parts);

                if (Map.TryGetValue(path, out metadata) && metadata != null && metadata.Type == typeof(T))
                    return true;

                metadata = null;
                return false;
            }
            /// <summary>
            /// Gets the ModAsset mapped to the given relative path.
            /// </summary>
            /// <param name="path">The relative asset path.</param>
            /// <param name="includeDirs">Whether to include directories or not.</param>
            /// <returns>The resulting mod asset meta object, or null.</returns>
            public static ModAsset Get<T>(string path, bool includeDirs = false) {
                ModAsset metadata;
                if (TryGet<T>(path, out metadata, includeDirs))
                    return metadata;
                return null;
            }

            /// <summary>
            /// Adds a new mapping for the given relative content path.
            /// </summary>
            /// <param name="path">The relative asset path.</param>
            /// <param name="metadata">The matching mod asset meta object.</param>
            public static void Add(string path, ModAsset metadata) {
                path = path.Replace('\\', '/');

                if (metadata != null) {
                    if (metadata.Type == null)
                        path = GuessType(path, out metadata.Type, out metadata.Format);
                    metadata.PathVirtual = path;
                }
                string prefix = metadata?.Source?.Name;

                if (metadata != null && metadata.Type == typeof(AssetTypeDirectory) && !(metadata is ModAssetBranch))
                    return;

                // We want our new mapping to replace the previous one, but need to replace the previous one in the shadow structure.
                ModAsset metadataPrev;
                if (!Map.TryGetValue(path, out metadataPrev))
                    metadataPrev = null;

                if (metadata == null && metadataPrev != null && metadataPrev.Type == typeof(AssetTypeDirectory))
                    return;

                if (metadata == null) {
                    Map[path] = null;
                    if (prefix != null)
                        Map[$"{prefix}:/{path}"] = null;

                } else {
                    Map[path] = metadata;
                    if (prefix != null)
                        Map[$"{prefix}:/{path}"] = metadata;
                }

                // If we're not already the highest level shadow "node"...
                if (path != "") {
                    // Add directories automatically.
                    string pathDir = Path.GetDirectoryName(path).Replace('\\', '/');
                    ModAsset metadataDir;
                    if (!Map.TryGetValue(pathDir, out metadataDir)) {
                        metadataDir = new ModAssetBranch {
                            PathVirtual = pathDir,
                            Type = typeof(AssetTypeDirectory)
                        };
                        Add(pathDir, metadataDir);
                    }
                    // If a previous mapping exists, replace it in the shadow structure.
                    int metadataPrevIndex = metadataDir.Children.IndexOf(metadataPrev);
                    if (metadataPrevIndex != -1) {
                        if (metadata == null) {
                            metadataDir.Children.RemoveAt(metadataPrevIndex);
                        } else {
                            metadataDir.Children[metadataPrevIndex] = metadata;
                        }
                    } else {
                        metadataDir.Children.Add(metadata);
                    }
                }
            }

            /// <summary>
            /// Invoked when GuessType can't guess the asset format / type.
            /// </summary>
            public static event TypeGuesser OnGuessType;
            /// <summary>
            /// Guess the file type and format based on its path. 
            /// </summary>
            /// <param name="file">The relative asset path.</param>
            /// <param name="type">The file type.</param>
            /// <param name="format">The file format (file ending).</param>
            /// <returns>The passed asset path, trimmed if required.</returns>
            public static string GuessType(string file, out Type type, out string format) {
                type = typeof(object);
                format = Path.GetExtension(file) ?? "";
                if (format.Length >= 1)
                    format = format.Substring(1);

                if (file.EndsWith(".dll")) {
                    type = typeof(AssetTypeAssembly);

                } else if (file.EndsWith(".png")) {
                    type = typeof(Texture2D);
                    file = file.Substring(0, file.Length - 4);

                } else if (file.EndsWith(".obj")) {
                    type = typeof(ObjModel);
                    file = file.Substring(0, file.Length - 4);

                } else if (file.EndsWith(".yaml")) {
                    type = typeof(AssetTypeYaml);
                    file = file.Substring(0, file.Length - 5);
                    format = ".yml";
                } else if (file.EndsWith(".yml")) {
                    type = typeof(AssetTypeYaml);
                    file = file.Substring(0, file.Length - 4);

                } else if (file.EndsWith(".xml")) {
                    type = typeof(AssetTypeXml);
                    file = file.Substring(0, file.Length - 4);

                } else if (file.StartsWith("Dialog/") && file.EndsWith(".txt")) {
                    type = typeof(AssetTypeDialog);
                    file = file.Substring(0, file.Length - 4);

                } else if (file.StartsWith("Dialog/") && file.EndsWith(".txt.export")) {
                    type = typeof(AssetTypeDialog);
                    file = file.Substring(0, file.Length - 7);

                } else if (file.StartsWith("Maps/") && file.EndsWith(".bin")) {
                    type = typeof(AssetTypeMap);
                    file = file.Substring(0, file.Length - 4);

                } else if (file.StartsWith("Tutorials/") && file.EndsWith(".bin")) {
                    type = typeof(AssetTypeTutorial);
                    file = file.Substring(0, file.Length - 4);

                } else if (file.EndsWith(".bank")) {
                    type = typeof(AssetTypeBank);
                    file = file.Substring(0, file.Length - 5);
                } else if (file.EndsWith(".guids.txt")) {
                    type = typeof(AssetTypeGUIDs);
                    file = file.Substring(0, file.Length - 4);
                } else if (file.EndsWith(".GUIDs.txt")) { // Default FMOD casing
                    type = typeof(AssetTypeGUIDs);
                    file = file.Substring(0, file.Length - 4 - 6);
                    file += ".guids";

                } else if (file.EndsWith(".txt")) {
                    type = typeof(AssetTypeText);
                    file = file.Substring(0, file.Length - 4);

                } else if (file.EndsWith(".lua")) {
                    type = typeof(AssetTypeLua);
                    file = file.Substring(0, file.Length - 4);

                } else if (OnGuessType != null) {
                    // Allow mods to parse custom types.
                    Delegate[] ds = OnGuessType.GetInvocationList();
                    for (int i = 0; i < ds.Length; i++) {
                        Type typeMod;
                        string formatMod;
                        string fileMod = ((TypeGuesser) ds[i])(file, out typeMod, out formatMod);
                        if (fileMod == null || typeMod == null || formatMod == null)
                            continue;
                        file = fileMod;
                        type = typeMod;
                        format = formatMod;
                        break;
                    }
                }

                return file;
            }

            /// <summary>
            /// Recrawl all currently loaded mods and recreate the content mappings. If you want to apply the new mapping, call Reprocess afterwards.
            /// </summary>
            [Obsolete("Mod content should no longer be recrawled manually.")]
            public static void Recrawl() {
                Map.Clear();

                for (int i = 0; i < Mods.Count; i++) {
                    ModContent mod = Mods[i];
                    mod.List.Clear();
                    mod.Map.Clear();
                    Crawl(mod);
                }
            }

            public static void Update(ModAsset prev, ModAsset next) {
                if (prev != null) {
                    foreach (object target in prev.Targets) {
                        if (target is MTexture mtex) {
                            AssetReloadScene.Do($"Unloading texture {Path.GetFileName(prev.PathVirtual)}", () => {
                                mtex.UndoOverride(prev);
                            });
                        }
                    }

                    if (next == null || prev.PathVirtual != next.PathVirtual)
                        Add(prev.PathVirtual, null);
                }


                if (next != null) {
                    Add(next.PathVirtual, next);
                    AssetReloadScene.Do($"Loading {Path.GetFileName(next.PathVirtual)}", () => {
                        foreach (WeakReference weakref in LoadedAssets) {
                            object target = weakref.Target;
                            if (!weakref.IsAlive)
                                return;

                            Process(target, next);
                        }
                    });

                    foreach (ModAsset child in next.Children)
                        Update(null, child);
                }
            }

            /// <summary>
            /// Crawl through the content mod and automatically fill the mod asset map.
            /// </summary>
            /// <param name="meta">The content mod to crawl through.</param>
            public static void Crawl(ModContent meta) {
                if (!Mods.Contains(meta))
                    Mods.Add(meta);
                meta._Crawl();
            }

            /// <summary>
            /// Process an asset and register it for further reprocessing in the future.
            /// Apply any mod-related changes to the asset based on the existing mod asset meta map.
            /// </summary>
            /// <param name="asset">The asset to process.</param>
            /// <param name="assetNameFull">The "full name" of the asset, preferable the relative asset path.</param>
            /// <returns>The processed asset.</returns>
            public static void Process(object asset, string assetNameFull) {
                if (DumpOnLoad)
                    Dump(assetNameFull, asset);

                string assetName = assetNameFull;
                if (assetName.StartsWith(PathContentOrig)) {
                    assetName = assetName.Substring(PathContentOrig.Length + 1);
                }

                int loadedIndex = LoadedAssetPaths.IndexOf(assetName);
                if (loadedIndex == -1) {
                    LoadedAssetPaths.Add(assetName);
                    LoadedAssetFullPaths.Add(assetNameFull);
                    LoadedAssets.Add(new WeakReference(asset));
                } else {
                    LoadedAssets[loadedIndex] = new WeakReference(asset);
                }

                Process(asset, Get(assetName, true));
            }

            public static void Process(object asset, ModAsset mapping) {
                if (asset == null || mapping == null)
                    return;

                if (asset is Atlas atlas) {
                    atlas.Ingest(mapping);
                }
            }

            /// <summary>
            /// Dump all dumpable game content into PathDUMP.
            /// </summary>
            public static void DumpAll() {
                bool prevDumpOnLoad = DumpOnLoad;
                DumpOnLoad = true;
                // TODO: Load and dump all other assets in original Content directory.

                // Dump atlases.

                // Noel on Discord:
                // not using it for the celeste assets but the "crunch" atlas packer is open source: https://github.com/ChevyRay/crunch
                // all celeste graphic assets use the Packer or PackerNoAtlas one tho

                // TODO: Find how to differentiate between Packer and PackerNoAtlas
                foreach (string file in Directory.EnumerateFiles(Path.Combine(PathContentOrig, "Graphics", "Atlases"), "*.meta", SearchOption.AllDirectories)) {
                    Logger.Log(LogLevel.Verbose, "dump-all-atlas-meta", "file: " + file);
                    // THIS IS HORRIBLE.
                    try {
                        Atlas.FromAtlas(file.Substring(0, file.Length - 5), Atlas.AtlasDataFormat.Packer).Dispose();
                    } catch {
                        Atlas.FromAtlas(file.Substring(0, file.Length - 5), Atlas.AtlasDataFormat.PackerNoAtlas).Dispose();
                    }
                }

                DumpOnLoad = prevDumpOnLoad;
            }

            /// <summary>
            /// Dump the given asset into an user-friendly and mod-compatible format.
            /// </summary>
            /// <param name="assetNameFull">The "full name" of the asset, preferable the relative asset path.</param>
            /// <param name="asset">The asset to process.</param>
            public static void Dump(string assetNameFull, object asset) {
                string assetName = assetNameFull;
                if (assetName.StartsWith(PathContentOrig)) {
                    assetName = assetName.Substring(PathContentOrig.Length + 1);
                } else if (File.Exists(assetName))
                    return; // Don't dump absolutely loaded files.

                string pathDump = Path.Combine(PathDUMP, assetName);
                Directory.CreateDirectory(Path.GetDirectoryName(pathDump));

                if (asset is IMeta) {
                    if (!File.Exists(pathDump + ".meta.yaml"))
                        using (Stream stream = File.OpenWrite(pathDump + ".meta.yaml"))
                        using (StreamWriter writer = new StreamWriter(stream))
                            YamlHelper.Serializer.Serialize(writer, asset, asset.GetType());

                } else if (asset is Texture2D) {
                    Texture2D tex = (Texture2D) asset;
                    if (!File.Exists(pathDump + ".png"))
                        using (Stream stream = File.OpenWrite(pathDump + ".png"))
                            tex.SaveAsPng(stream, tex.Width, tex.Height);

                } else if (asset is VirtualTexture) {
                    VirtualTexture tex = (VirtualTexture) asset;
                    Dump(assetName, tex.Texture);

                } else if (asset is MTexture) {
                    MTexture tex = (MTexture) asset;
                    // Always copy even if !.IsSubtexture() as we need to Postdivide()
                    using (Texture2D region = tex.GetPaddedSubtextureCopy().Postdivide())
                        Dump(assetName, region);

                    /*
                    if (tex.DrawOffset.X != 0 || tex.DrawOffset.Y != 0 ||
                        tex.Width != tex.ClipRect.Width || tex.Height != tex.ClipRect.Height
                    ) {
                        Dump(assetName, new MTextureMeta {
                            X = (int) Math.Round(tex.DrawOffset.X),
                            Y = (int) Math.Round(tex.DrawOffset.Y),
                            Width = tex.Width,
                            Height = tex.Height
                        });
                    }
                    */

                } else if (asset is Atlas) {
                    Atlas atlas = (Atlas) asset;

                    /*
                    for (int i = 0; i < atlas.Sources.Count; i++) {
                        VirtualTexture source = atlas.Sources[i];
                        string name = source.Name;

                        if (name.StartsWith(assetNameFull))
                            name = assetName + "_s_" + name.Substring(assetNameFull.Length);
                        else
                            name = Path.Combine(assetName + "_s", name);
                        if (name.EndsWith(".data") || name.EndsWith(".meta"))
                            name = name.Substring(0, name.Length - 5);

                        Dump(name, source);
                    }
                    */

                    Dictionary<string, MTexture> textures = atlas.GetTextures();
                    foreach (KeyValuePair<string, MTexture> kvp in textures) {
                        string name = kvp.Key;
                        MTexture source = kvp.Value;
                        Dump(Path.Combine(assetName, name.Replace('/', Path.DirectorySeparatorChar)), source);
                    }
                }

                // TODO: Dump more asset types if required.
            }

        }
    }
}
