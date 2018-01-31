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

            public static string PathContent { get; internal set; }
            public static string PathPatches { get; internal set; }
            public static string PathPrefixPatches { get; internal set; }
            public static string PathDialog { get; internal set; }
            public static string PathPrefixDialog { get; internal set; }

            public readonly static IList<ContentModMetadata> Mods = new List<ContentModMetadata>();

            public readonly static IDictionary<string, AssetMetadata> Map = new FastDictionary<string, AssetMetadata>();
            public readonly static IDictionary<string, AssetMetadata> MapDirs = new FastDictionary<string, AssetMetadata>();
            public readonly static IDictionary<string, List<AssetMetadata>> MapPatches = new FastDictionary<string, List<AssetMetadata>>();

            public readonly static IDictionary<string, object> Cache = new FastDictionary<string, object>();
            public readonly static HashSet<Type> CacheableTypes = new HashSet<Type>() {
                Types.Texture,
                Types.Texture2D,
                Types.ObjModel
            };

            internal static void Initialize() {
                Celeste.Instance.Content = new EverestContentManager(Celeste.Instance.Content);

                Directory.CreateDirectory(PathContent = Path.Combine(PathGame, "ModContent"));

                Directory.CreateDirectory(PathPatches = Path.Combine(PathContent, PathPrefixPatches = "Patches"));
                PathPrefixPatches += "/";

                Directory.CreateDirectory(PathDialog = Path.Combine(PathContent, PathPrefixDialog = "Texts"));
                PathPrefixDialog += "/";

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

            public static bool TryGetMappedPatches(string path, out List<AssetMetadata> metadatas) {
                if (MapPatches.TryGetValue(path, out metadatas)) return true;
                if (MapPatches.TryGetValue(path.ToLowerInvariant(), out metadatas)) return true;

                return false;
            }
            public static List<AssetMetadata> GetMappedPatches(string path) {
                List<AssetMetadata> metadatas;
                TryGetMappedPatches(path, out metadatas);
                return metadatas;
            }

            public static AssetMetadata AddMapping(string path, AssetMetadata metadata) {
                path = path.Replace('\\', '/');
                if (metadata.AssetType == null)
                    path = ParseType(path, out metadata.AssetType, out metadata.AssetFormat, out metadata.IsPatch);
                if (metadata.IsPatch)
                    return AddMappingPatch(path, metadata);
                if (metadata.AssetType == Types.AssetTypeDirectory)
                    return MapDirs[path] = MapDirs[path.ToLowerInvariant()] = metadata;

                return Map[path] = Map[path.ToLowerInvariant()] = metadata;
            }

            public static AssetMetadata AddMappingPatch(string path, AssetMetadata metadata) {
                List<AssetMetadata> metadatas;
                if (!TryGetMappedPatches(path, out metadatas))
                    MapPatches[path] = MapPatches[path.ToLowerInvariant()] = metadatas = new List<AssetMetadata>();
                metadatas.Add(metadata);
                return metadata;
            }

            public static string ParseType(string file, out Type type, out string format, out bool isPatch) {
                type = Types.Object;
                format = file.Length < 4 ? null : file.Substring(file.Length - 3);
                isPatch = false;

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

                if (file.EndsWith(".patch")) {
                    isPatch = true;
                    file = file.Substring(0, file.Length - 6);
                }

                return file;
            }

            public static void Recrawl() {
                Cache.Clear();

                Map.Clear();
                MapDirs.Clear();
                MapPatches.Clear();

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

                if (Path.GetFileName(dir).StartsWith("DUMP"))
                    return;
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

            public static T Patch<T>(string path, T asset) {
                List<AssetMetadata> mapping = GetMappedPatches(path);
                if (mapping == null || mapping.Count == 0)
                    return asset;

                if (asset is Texture2D)
                    return (T) (object) (asset as Texture2D).Patch(GetMappedPatches(path));

                // TODO: Allow mods to patch custom types.

                return asset;
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
