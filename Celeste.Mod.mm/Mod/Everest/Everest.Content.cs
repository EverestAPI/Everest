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

namespace Celeste.Mod {
    // Special meta types.
    public sealed class AssetTypeDirectory { private AssetTypeDirectory() { } }
    public sealed class AssetTypeAssembly { private AssetTypeAssembly() { } }
    public sealed class AssetTypeYaml { private AssetTypeYaml() { } }
    public sealed class AssetTypeXml { private AssetTypeXml() { } }
    public sealed class AssetTypeText { private AssetTypeText() { } }
    public sealed class AssetTypeDialog { private AssetTypeDialog() { } }
    public sealed class AssetTypeMap { private AssetTypeMap() { } }
    public sealed class AssetTypeBank { private AssetTypeBank() { } }
    public sealed class AssetTypeGUIDs { private AssetTypeGUIDs() { } }

    // Delegate types.
    public delegate string TypeGuesser(string file, out Type type, out string format);

    // Source types.
    public abstract class ModContent {
        public virtual string DefaultName { get; }
        private string _Name;
        public string Name {
            get => _Name ?? DefaultName;
            set => _Name = value;
        }

        public List<ModAsset> List = new List<ModAsset>();
        public Dictionary<string, ModAsset> Map = new Dictionary<string, ModAsset>();

        protected abstract void Crawl();
        internal void _Crawl() => Crawl();

        protected virtual void Add(string path, ModAsset asset) {
            asset = Everest.Content.Add(path, asset);
            List.Add(asset);
            Map[asset.PathVirtual] = asset;
        }
    }

    public class FileSystemModContent : ModContent {
        /// <summary>
        /// The path to the mod directory.
        /// </summary>
        public string Path;

        public FileSystemModContent(string path) {
            Path = path;
        }

        protected override void Crawl() => Crawl(null);

        protected virtual void Crawl(string dir, string root = null) {
            if (dir == null)
                dir = Path;
            if (root == null)
                root = dir;
            string[] files = Directory.GetFiles(dir);
            for (int i = 0; i < files.Length; i++) {
                string file = files[i];
                Add(file.Substring(root.Length + 1), new FileSystemModAsset(this, file));
            }
            files = Directory.GetDirectories(dir);
            for (int i = 0; i < files.Length; i++) {
                string file = files[i];
                Crawl(file, root);
            }
        }
    }

    public class MapBinsInModsModContent : ModContent {
        /// <summary>
        /// The path to the mod directory.
        /// </summary>
        public string Path;

        public MapBinsInModsModContent(string path) {
            Path = path;
        }

        protected override void Crawl() {
            string[] files = Directory.GetFiles(Path);
            for (int i = 0; i < files.Length; i++) {
                string file = files[i];
                if (!file.EndsWith(".bin"))
                    continue;
                Add("Maps/" + file.Substring(Path.Length + 1), new MapBinsInModsModAsset(this, file));
            }
        }
    }

    public class AssemblyModContent : ModContent {
        /// <summary>
        /// The assembly containing the mod content as resources.
        /// </summary>
        public Assembly Assembly;

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
        /// <summary>
        /// The path to the archive containing the mod content.
        /// </summary>
        public string Path;

        public ZipModContent(string path) {
            Path = path;
        }

        protected override void Crawl() {
            using (ZipFile zip = new ZipFile(Path)) {
                foreach (ZipEntry entry in zip.Entries) {
                    string entryName = entry.FileName.Replace('\\', '/');
                    if (entryName.EndsWith("/"))
                        continue;
                    Add(entryName, new ZipModAsset(this, entryName));
                }
            }
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
            /// <summary>
            /// Mod content mapping, directories only. Use Everest.Content.Add, Get, and TryGet where applicable instead.
            /// </summary>
            public readonly static Dictionary<string, ModAsset> MapDirs = new Dictionary<string, ModAsset>();

            internal readonly static List<string> LoadedAssetPaths = new List<string>();
            internal readonly static List<string> LoadedAssetFullPaths = new List<string>();
            internal readonly static List<WeakReference> LoadedAssets = new List<WeakReference>();

            internal static void Initialize() {
                Celeste.Instance.Content = new EverestContentManager(Celeste.Instance.Content);

                Directory.CreateDirectory(PathContentOrig = Path.Combine(PathGame, Celeste.Instance.Content.RootDirectory));
                Directory.CreateDirectory(PathDUMP = Path.Combine(PathEverest, "ModDUMP"));

                if (_DumpAll)
                    DumpAll();

                if (Flags.Disabled)
                    return;

                Crawl(new AssemblyModContent(typeof(Everest).Assembly) {
                    Name = "Everest"
                });
                Crawl(new MapBinsInModsModContent(Path.Combine(PathEverest, "Mods")));
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

                if (includeDirs && MapDirs.TryGetValue(path, out metadata))
                    return true;
                if (Map.TryGetValue(path, out metadata))
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

                if (includeDirs && MapDirs.TryGetValue(path, out metadata) && metadata.Type == typeof(T))
                    return true;
                if (Map.TryGetValue(path, out metadata) && metadata.Type == typeof(T))
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
            /// <returns>The passed mod asset meta object.</returns>
            public static ModAsset Add(string path, ModAsset metadata) {
                path = path.Replace('\\', '/');
                
                if (metadata.Type == null)
                    path = GuessType(path, out metadata.Type, out metadata.Format);

                metadata.PathVirtual = path;
                string prefix = metadata.Source?.Name;

                // We want our new mapping to replace the previous one, but need to replace the previous one in the shadow structure.
                ModAsset metadataPrev;
                if (!Map.TryGetValue(path, out metadataPrev))
                    metadataPrev = null;

                // Hardcoded case: Handle directories separately.
                if (metadata.Type == typeof(AssetTypeDirectory)) {
                    MapDirs[path] = metadata;
                    if (prefix != null)
                        MapDirs[prefix + ":" + path] = metadata;
                } else {
                    Map[path] = metadata;
                    if (prefix != null)
                        Map[prefix + ":" + path] = metadata;
                }

                // If we're not already the highest level shadow "node"...
                if (path != "") {
                    // Add directories automatically.
                    string pathDir = Path.GetDirectoryName(path).Replace('\\', '/');
                    ModAsset metadataDir;
                    if (!MapDirs.TryGetValue(pathDir, out metadataDir)) {
                        metadataDir = new ModAssetBranch {
                            PathVirtual = pathDir,
                            Type = typeof(AssetTypeDirectory)
                        };
                        Add(pathDir, metadataDir);
                    }
                    // If a previous mapping exists, replace it in the shadow structure.
                    int metadataPrevIndex = metadataDir.Children.IndexOf(metadataPrev);
                    if (metadataPrevIndex != -1)
                        metadataDir.Children[metadataPrevIndex] = metadata;
                    else
                        metadataDir.Children.Add(metadata);
                }

                return metadata;
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

                } else if (file.StartsWith("Maps/") && file.EndsWith(".bin")) {
                    type = typeof(AssetTypeMap);
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
            public static void Recrawl() {
                Map.Clear();
                MapDirs.Clear();

                for (int i = 0; i < Mods.Count; i++) {
                    ModContent mod = Mods[i];
                    mod.List.Clear();
                    mod.Map.Clear();
                    Crawl(mod);
                }
            }

            /// <summary>
            /// Reprocess all loaded / previously processed assets, re-applying any changes after a recrawl.
            /// </summary>
            public static void Reprocess() {
                for (int i = 0; i < LoadedAssets.Count; i++) {
                    WeakReference weak = LoadedAssets[i];
                    if (!weak.IsAlive)
                        continue;
                    Process(weak.Target, LoadedAssetFullPaths[i]);
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
            /// Invoked when content is being processed (most likely on load), allowing you to manipulate it.
            /// </summary>
            public static event Func<object, string, object> OnProcess;
            /// <summary>
            /// Process an asset and register it for further reprocessing in the future.
            /// Apply any mod-related changes to the asset based on the existing mod asset meta map.
            /// </summary>
            /// <param name="asset">The asset to process.</param>
            /// <param name="assetNameFull">The "full name" of the asset, preferable the relative asset path.</param>
            /// <returns>The processed asset.</returns>
            public static object Process(object asset, string assetNameFull) {
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

                if (asset is Atlas) {
                    Atlas atlas = asset as Atlas;
                    ModAsset mapping;

                    mapping = Get(assetName + "LQ", true);
                    if (mapping != null && mapping.Type == typeof(AssetTypeDirectory)) {
                        atlas.Ingest(mapping);
                    }

                    mapping = Get(assetName, true);
                    if (mapping != null && mapping.Type == typeof(AssetTypeDirectory)) {
                        atlas.Ingest(mapping);
                    }

                    return asset;
                }

                return OnProcess?.InvokePassing(asset, assetNameFull) ?? asset;
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
                    using (Texture2D region = tex.GetSubtextureCopy().Postdivide())
                        Dump(assetName, region);

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
