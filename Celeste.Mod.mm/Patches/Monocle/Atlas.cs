#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Logger = Celeste.Mod.Logger;

namespace Monocle {
    class patch_Atlas : Atlas {

        // We're effectively in Atlas, but still need to "expose" private fields to our mod.
        private Dictionary<string, MTexture> textures;
        public Dictionary<string, MTexture> Textures => textures;

        public string DataMethod;
        public string DataPath;
        public string[] DataPaths;
        public AtlasDataFormat? DataFormat;

        private static extern void orig_ReadAtlasData(Atlas atlas, string path, AtlasDataFormat format);
        private static void ReadAtlasData(Atlas atlas, string path, AtlasDataFormat format) {
            string pathFull = Path.Combine(Engine.ContentDirectory, path);

            // If the file doesn't exist, don't add any data to the atlas.
            switch (format) {
                case AtlasDataFormat.TexturePacker_Sparrow:
                case AtlasDataFormat.CrunchXml:
                case AtlasDataFormat.CrunchBinary:
                    // These formats don't append any file extension.
                    if (!File.Exists(pathFull))
                        return;
                    break;

                case AtlasDataFormat.CrunchXmlOrBinary:
                    // Check against both .bin and .xml paths, as the game reads whichever exists.
                    if (!File.Exists(pathFull + ".bin") && !File.Exists(pathFull + ".xml"))
                        return;
                    break;

                case AtlasDataFormat.CrunchBinaryNoAtlas:
                    // This appends .bin to the path for whatever reason (compared to CrunchBinary).
                    if (!File.Exists(pathFull + ".bin"))
                        return;
                    break;

                case AtlasDataFormat.Packer:
                case AtlasDataFormat.PackerNoAtlas:
                    // The only format used by the game.
                    if (!File.Exists(pathFull + ".meta"))
                        return;
                    break;

                default:
                    // Unsupported format. Let's avoid crashing.
                    return;
            }

            orig_ReadAtlasData(atlas, path, format);
        }

        public static extern Atlas orig_FromAtlas(string path, AtlasDataFormat format);
        public static new Atlas FromAtlas(string path, AtlasDataFormat format) {
            patch_Atlas atlas = (patch_Atlas) orig_FromAtlas(path, format);
            atlas.DataMethod = "FromAtlas";
            atlas.DataPath = path;
            atlas.DataFormat = format;
            Everest.Content.Process(atlas, atlas.DataPath);
            Everest.Events.Atlas.Load(atlas);
            return atlas;
        }

        public static extern Atlas orig_FromMultiAtlas(string rootPath, string[] dataPath, AtlasDataFormat format);
        public static new Atlas FromMultiAtlas(string rootPath, string[] dataPath, AtlasDataFormat format) {
            patch_Atlas atlas = (patch_Atlas) orig_FromMultiAtlas(rootPath, dataPath, format);
            atlas.DataMethod = "FromMultiAtlas";
            atlas.DataPath = rootPath;
            atlas.DataPaths = dataPath;
            atlas.DataFormat = format;
            Everest.Content.Process(atlas, atlas.DataPath);
            Everest.Events.Atlas.Load(atlas);
            return atlas;
        }

        public static extern Atlas orig_FromMultiAtlas(string rootPath, string filename, AtlasDataFormat format);
        public static new Atlas FromMultiAtlas(string rootPath, string filename, AtlasDataFormat format) {
            patch_Atlas atlas = (patch_Atlas) orig_FromMultiAtlas(rootPath, filename, format);
            atlas.DataMethod = "FromMultiAtlas";
            atlas.DataPath = rootPath;
            atlas.DataPaths = new string[] { filename };
            atlas.DataFormat = format;
            Everest.Content.Process(atlas, atlas.DataPath);
            Everest.Events.Atlas.Load(atlas);
            return atlas;
        }

        public static extern Atlas orig_FromDirectory(string path);
        public static new Atlas FromDirectory(string path) {
            patch_Atlas atlas = (patch_Atlas) orig_FromDirectory(path);
            atlas.DataMethod = "FromDirectory";
            atlas.DataPath = path;
            Everest.Content.Process(atlas, atlas.DataPath);
            Everest.Events.Atlas.Load(atlas);
            return atlas;
        }

    }
    public static class AtlasExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Get the internal string-MTexture dictionary.
        /// </summary>
        public static Dictionary<string, MTexture> GetTextures(this Atlas self)
            => ((patch_Atlas) self).Textures;

        /// <summary>
        /// Get the method with which this atlas was loaded.
        /// </summary>
        public static string GetDataMethod(this Atlas self)
            => ((patch_Atlas) self).DataMethod;

        /// <summary>
        /// Get the path from which this atlas was loaded.
        /// </summary>
        public static string GetDataPath(this Atlas self)
            => ((patch_Atlas) self).DataPath;

        /// <summary>
        /// If the atlas was loaded with FromMultiAtlas, get the paths from which this atlas was loaded.
        /// </summary>
        public static string[] GetDataPaths(this Atlas self)
            => ((patch_Atlas) self).DataPaths;

        /// <summary>
        /// Get the atlas data format, or none in case of directory atlases.
        /// </summary>
        public static Atlas.AtlasDataFormat? GetDataFormat(this Atlas self)
            => ((patch_Atlas) self).DataFormat;

        /// <summary>
        /// Feed the given ModAsset into the atlas.
        /// </summary>
        public static void Ingest(this Atlas self, ModAsset asset) {
            Logger.Log(LogLevel.Verbose, "Atlas.Ingest", $"{self.GetDataPath()} + {asset.PathMapped}");

            // Crawl through all child assets.
            if (asset.AssetType == typeof(AssetTypeDirectory)) {
                foreach (ModAsset child in asset.Children)
                    self.Ingest(child);
                return;
            }

            // Forcibly add the mod content to the atlas.
            if (asset.AssetType == typeof(Texture2D)) {
                string parentPath = self.GetDataPath();
                if (parentPath.StartsWith(Everest.Content.PathContentOrig))
                    parentPath = parentPath.Substring(Everest.Content.PathContentOrig.Length + 1);
                parentPath = parentPath.Replace('\\', '/');

                string path = asset.PathMapped;
                if (!path.StartsWith(parentPath))
                    return;
                path = path.Substring(parentPath.Length + 1);

                VirtualTexture replacementV = VirtualContentExt.CreateTexture(asset);
                MTexture replacement;
                MTextureMeta meta = asset.GetMeta<MTextureMeta>();

                Dictionary<string, MTexture> textures = self.GetTextures();
                MTexture existing;
                if (textures.TryGetValue(path, out existing)) {
                    // We're the currently active overlay.
                    if (existing.Texture.GetMetadata() == asset)
                        return;

                    if (meta != null) {
                        // Apply width and height from existing meta.
                        existing.AddOverride(replacementV, new Vector2(meta.X, meta.Y), meta.Width, meta.Height);
                    } else {
                        // Keep width and height from existing instance.
                        existing.AddOverride(replacementV, existing.DrawOffset, existing.Width, existing.Height);
                    }

                    replacement = existing;

                } else {
                    if (meta != null) {
                        // Apply width and height from existing meta.
                        replacement = new MTexture(replacementV, new Vector2(meta.X, meta.Y), meta.Width, meta.Height);
                    } else {
                        // Apply width and height from replacement texture.
                        replacement = new MTexture(replacementV);
                    }
                    // TODO: What's with the AtlasPath? Seems to stem from an atlas metadata property...
                }

                self[path] = replacement;
                return;
            }
        }

    }
}
