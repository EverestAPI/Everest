using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Monocle;
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
        public static class Content {

            /// <summary>
            /// Special meta types.
            /// </summary>
            public sealed class AssetTypeDirectory { private AssetTypeDirectory() { } }
            public sealed class AssetTypeAssembly { private AssetTypeAssembly() { } }

            /// <summary>
            /// Cached common type references. Microoptimization to replace ldtoken and token to ref conversion call with ldfld.
            /// </summary>
            internal static class Types {
                public readonly static Type Object = typeof(object);

                public readonly static Type Content = typeof(Content);

                public readonly static Type AssetTypeDirectory = typeof(AssetTypeDirectory);
                public readonly static Type AssetTypeAssembly = typeof(AssetTypeAssembly);

                public readonly static Type Texture = typeof(Texture);
                public readonly static Type Texture2D = typeof(Texture2D);

                public readonly static Type ObjModel = typeof(ObjModel);
            }

            /// <summary>
            /// Should Everest dump all game assets into a user-friendly format on load?
            /// </summary>
            public static bool DumpOnLoad = false;
            internal static bool _DumpAll = false;

            public static string PathContentOrig { get; internal set; }
            public static string PathContent { get; internal set; }
            public static string PathDUMP { get; internal set; }

            public readonly static IList<ContentModMetadata> Mods = new List<ContentModMetadata>();

            public readonly static IDictionary<string, AssetMetadata> Map = new FastDictionary<string, AssetMetadata>();
            public readonly static IDictionary<string, AssetMetadata> MapDirs = new FastDictionary<string, AssetMetadata>();

            public readonly static IDictionary<string, object> Cache = new FastDictionary<string, object>();
            public readonly static HashSet<Type> CacheableTypes = new HashSet<Type>() {
                Types.Texture,
                Types.Texture2D,
                Types.ObjModel
            };

            internal static void Initialize() {
                Celeste.Instance.Content = new EverestContentManager(Celeste.Instance.Content);

                Directory.CreateDirectory(PathContentOrig = Path.Combine(PathGame, Celeste.Instance.Content.RootDirectory));
                Directory.CreateDirectory(PathContent = Path.Combine(PathGame, "ModContent"));
                Directory.CreateDirectory(PathDUMP = Path.Combine(PathGame, "ModDUMP"));

                if (_DumpAll) {
                    bool prevDumpOnLoad = DumpOnLoad;
                    DumpOnLoad = true;
                    // TODO: Load and dump all other assets in original Content directory.

                    // Dump atlases.

                    // Noel on Discord:
                    // not using it for the celeste assets but the "crunch" atlas packer is open source: https://github.com/ChevyRay/crunch
                    // all celeste graphic assets use the Packer or PackerNoAtlas one tho

                    // TODO: Find how to differentiate between Packer and PackerNoAtlas
                    foreach (string file in Directory.EnumerateFiles(Path.Combine(PathContentOrig, "Graphics", "Atlases"), "*.meta", SearchOption.AllDirectories)) {
                        Logger.Log("dump-all-atlas-meta", "file: " + file);
                        // THIS IS HORRIBLE.
                        try {
                            Atlas.FromAtlas(file.Substring(0, file.Length - 5), Atlas.AtlasDataFormat.Packer).Dispose();
                        } catch {
                            Atlas.FromAtlas(file.Substring(0, file.Length - 5), Atlas.AtlasDataFormat.PackerNoAtlas).Dispose();
                        }
                    }

                    DumpOnLoad = prevDumpOnLoad;
                }

                Crawl(null, PathContent);
            }

            public static bool TryGetMapped(string path, out AssetMetadata metadata, bool includeDirs = false) {
                if (includeDirs) {
                    if (MapDirs.TryGetValue(path, out metadata)) return true;
                    if (MapDirs.TryGetValue(path.ToLowerInvariant(), out metadata)) return true;
                }
                if (Map.TryGetValue(path, out metadata)) return true;
                if (Map.TryGetValue(path.ToLowerInvariant(), out metadata)) return true;

                return false;
            }
            public static AssetMetadata GetMapped(string path) {
                AssetMetadata metadata;
                TryGetMapped(path, out metadata);
                return metadata;
            }

            public static AssetMetadata AddMapping(string path, AssetMetadata metadata) {
                path = path.Replace('\\', '/');
                if (metadata.AssetType == null)
                    path = ParseType(path, out metadata.AssetType, out metadata.AssetFormat);
                if (metadata.AssetType == Types.AssetTypeDirectory)
                    return MapDirs[path] = MapDirs[path.ToLowerInvariant()] = metadata;

                return Map[path] = Map[path.ToLowerInvariant()] = metadata;
            }

            public static string ParseType(string file, out Type type, out string format) {
                type = Types.Object;
                format = file.Length < 4 ? null : file.Substring(file.Length - 3);

                if (file.EndsWith(".dll")) {
                    type = Types.AssetTypeAssembly;

                } else if (file.EndsWith(".png")) {
                    type = Types.Texture2D;
                    file = file.Substring(0, file.Length - 4);
                } else if (file.EndsWith(".obj")) {
                    type = Types.ObjModel;
                    file = file.Substring(0, file.Length - 4);
                } else {
                    // TODO: Allow mods to parse custom types.
                }

                return file;
            }

            public static void Recrawl() {
                Cache.Clear();

                Map.Clear();
                MapDirs.Clear();

                for (int i = 0; i < Mods.Count; i++)
                    Crawl(Mods[i]);
            }

            public static void Crawl(ContentModMetadata meta) {
                if (meta.PathDirectory != null)
                    Crawl(meta, meta.PathDirectory);
                else if (meta.PathArchive != null)
                    using (Stream zipStream = File.OpenRead(meta.PathArchive))
                    using (ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
                        Crawl(meta, meta.PathArchive, zip);
                else if (meta.Assembly != null)
                    Crawl(meta, meta.Assembly);
            }

            public static void Crawl(ContentModMetadata meta, string dir, string root = null) {
                if (meta == null)
                    Mods.Add(meta = new ContentModMetadata() {
                        PathDirectory = dir
                    });

                if (root == null)
                    root = dir;
                string[] files = Directory.GetFiles(dir);
                for (int i = 0; i < files.Length; i++) {
                    string file = files[i];
                    AddMapping(file.Substring((root?.Length ?? 0) + 1), new AssetMetadata(file));
                }
                files = Directory.GetDirectories(dir);
                for (int i = 0; i < files.Length; i++) {
                    string file = files[i];
                    AddMapping(file.Substring((root?.Length ?? 0) + 1), new AssetMetadata(file) {
                        AssetType = Types.AssetTypeDirectory,
                        HasData = false
                    });
                    Crawl(meta, file, root);
                }
            }

            public static void Crawl(ContentModMetadata meta, Assembly asm) {
                if (meta == null)
                    Mods.Add(meta = new ContentModMetadata() {
                        Assembly = asm
                    });

                string[] resourceNames = asm.GetManifestResourceNames();
                for (int i = 0; i < resourceNames.Length; i++) {
                    string name = resourceNames[i];
                    int indexOfContent = name.IndexOf("Content");
                    if (indexOfContent < 0)
                        continue;
                    name = name.Substring(indexOfContent + 8);
                    AddMapping(name, new AssetMetadata(asm, resourceNames[i]));
                }
            }

            public static void Crawl(ContentModMetadata meta, string archive, ZipArchive zip) {
                if (meta == null)
                    Mods.Add(meta = new ContentModMetadata() {
                        PathArchive = archive
                    });

                foreach (ZipArchiveEntry entry in zip.Entries) {
                    string entryName = entry.FullName.Replace('\\', '/');
                    AddMapping(entryName, new AssetMetadata(archive, entryName) {
                        AssetType = entryName.EndsWith("/") ? typeof(AssetTypeDirectory) : null
                    });
                }
            }

            public static T Process<T>(string assetName, T asset) {
                if (DumpOnLoad)
                    Dump(assetName, asset);
                
                // TODO: Allow mods to process the asset at runtime.
                return asset;
            }

            public static void Dump(string assetNameFull, object asset) {
                string assetName = assetNameFull;
                if (assetName.StartsWith(PathContentOrig)) {
                    assetName = assetName.Substring(PathContentOrig.Length + 1);
                } else if (File.Exists(assetName))
                    return; // Don't dump absolutely loaded files.

                string pathDump = Path.Combine(PathDUMP, assetName);
                Directory.CreateDirectory(Path.GetDirectoryName(pathDump));

                if (asset is Texture2D) {
                    Texture2D tex = (Texture2D) asset;
                    if (!File.Exists(pathDump + ".png"))
                        using (Stream stream = File.OpenWrite(pathDump + ".png"))
                            tex.SaveAsPng(stream, tex.Width, tex.Height);

                } else if (asset is VirtualTexture) {
                    VirtualTexture tex = (VirtualTexture) asset;
                    Dump(assetName, tex.Texture);

                } else if (asset is MTexture) {
                    MTexture tex = (MTexture) asset;
                    if (!tex.IsSubtexture())
                        Dump(assetName, tex.Texture.Texture);
                    else
                        using (Texture2D region = tex.GetSubtextureCopy())
                            Dump(assetName, region);

                } else if (asset is Atlas) {
                    Atlas atlas = (Atlas) asset;

                    if (!File.Exists(pathDump + ".yaml")) {
                        // TODO: YAML metadata dump!
                    }

                    /*
                    for (int i = 0; i < atlas.Sources.Count; i++) {
                        VirtualTexture source = atlas.Sources[i];
                        string name = source.Name;

                        if (name.StartsWith(assetNameFull))
                            name = assetName + "_s_" + name.Substring(assetNameFull.Length);
                        else
                            name = Path.Combine(assetName + "_s", name);
                        if (name.EndsWith(".data"))
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

    public class ContentModMetadata {

        /// <summary>
        /// The path to the ZIP of the mod, if this is a .zip mod.
        /// </summary>
        public virtual string PathArchive { get; set; }

        /// <summary>
        /// The path to the directory of the mod, if this is a directory mod.
        /// </summary>
        public virtual string PathDirectory { get; set; }

        /// <summary>
        /// The assembly containing the resources, if the source is an assembly.
        /// </summary>
        public virtual Assembly Assembly { get; set; }

    }
}
