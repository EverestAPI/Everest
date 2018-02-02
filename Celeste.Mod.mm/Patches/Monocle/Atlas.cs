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

namespace Monocle {
    class patch_Atlas : Atlas {

        // We're effectively in Atlas, but still need to "expose" private fields to our mod.
        private Dictionary<string, MTexture> textures;
        public Dictionary<string, MTexture> Textures => textures;

        public string DataMethod;
        public string DataPath;
        public string[] DataPaths;
        public AtlasDataFormat? DataFormat;

        public static extern Atlas orig_FromAtlas(string path, AtlasDataFormat format);
        public static new Atlas FromAtlas(string path, AtlasDataFormat format) {
            patch_Atlas atlas = (patch_Atlas) orig_FromAtlas(path, format);
            atlas.DataMethod = "FromAtlas";
            atlas.DataPath = path;
            atlas.DataFormat = format;
            Everest.Content.Process(path, atlas);
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
            Everest.Content.Process(rootPath, atlas);
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
            Everest.Content.Process(rootPath, atlas);
            Everest.Events.Atlas.Load(atlas);
            return atlas;
        }

        public static extern Atlas orig_FromDirectory(string path);
        public static new Atlas FromDirectory(string path) {
            patch_Atlas atlas = (patch_Atlas) orig_FromDirectory(path);
            atlas.DataMethod = "FromDirectory";
            atlas.DataPath = path;
            Everest.Content.Process(path, atlas);
            Everest.Events.Atlas.Load(atlas);
            return atlas;
        }

    }
    public static class AtlasExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static Dictionary<string, MTexture> GetTextures(this Atlas self)
            => ((patch_Atlas) self).Textures;

        public static string GetDataMethod(this Atlas self)
            => ((patch_Atlas) self).DataMethod;

        public static string GetDataPath(this Atlas self)
            => ((patch_Atlas) self).DataPath;

        public static string[] GetDataPaths(this Atlas self)
            => ((patch_Atlas) self).DataPaths;

        public static Atlas.AtlasDataFormat? GetDataFormat(this Atlas self)
            => ((patch_Atlas) self).DataFormat;

        public static void Ingest(this Atlas self, AssetMetadata asset) {
            // Crawl through all child assets.
            if (asset.AssetType == Everest.Content.Types.AssetTypeDirectory) {
                foreach (AssetMetadata child in asset.Children)
                    self.Ingest(child);
                return;
            }

            // Forcibly add the mod content to the atlas.
            if (asset.AssetType == Everest.Content.Types.Texture2D) {
                string parentPath = self.GetDataPath();
                if (parentPath.StartsWith(Everest.Content.PathContentOrig))
                    parentPath = parentPath.Substring(Everest.Content.PathContentOrig.Length + 1);
                parentPath = parentPath.Replace('\\', '/');

                string path = asset.PathRelative;
                if (!path.StartsWith(parentPath))
                    return;
                path = path.Substring(parentPath.Length + 1);

                VirtualTexture replacementV = VirtualContentExt.CreateTexture(asset);
                MTexture replacement;
                AssetMetadata metaAsset;
                AtlasFrameMeta meta;

                Dictionary<string, MTexture> textures = self.GetTextures();
                MTexture existing;
                if (textures.TryGetValue(path, out existing)) {
                    // Apply width and height from existing instance.
                    replacement = new MTexture(replacementV, existing.DrawOffset, existing.Width, existing.Height);
                    replacement.SetAtlasPath(existing.AtlasPath);

                    // Unload the texture if no other reference to the same VirtualTexture texture remaining.
                    bool alive = false;
                    foreach (KeyValuePair<string, MTexture> other in textures) {
                        if (other.Key != path && other.Value.Texture == existing.Texture) {
                            alive = true;
                            break;
                        }
                    }
                    if (!alive)
                        existing.Unload();

                } else if (
                    Everest.Content.TryGet(asset.PathRelative + ".meta", out metaAsset) &&
                    metaAsset.TryDeserialize(out meta)
                ) {
                    // Read metadata if available and use it.
                    replacement = new MTexture(replacementV, new Vector2(meta.X, meta.Y), meta.Width, meta.Height);
                } else {
                    // Apply width and height from replacement texture.
                    replacement = new MTexture(replacementV);
                }

                self[path] = replacement;
                return;
            }
        }

    }
}
