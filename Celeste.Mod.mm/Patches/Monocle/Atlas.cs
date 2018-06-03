#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Core;
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

        public static Dictionary<string, MTexture> VTextureToMTextureMap;

        public string DataMethod;
        public string DataPath;
        public string[] DataPaths;
        public AtlasDataFormat? DataFormat;

        [MonoModReplace]
        private static void ReadAtlasData(Atlas _atlas, string path, AtlasDataFormat format) {
            if (VTextureToMTextureMap == null)
                VTextureToMTextureMap = new Dictionary<string, MTexture>();

            patch_Atlas atlas = (patch_Atlas) _atlas;

            string pathFull = Path.Combine(Engine.ContentDirectory, path);

            XmlDocument xmlDoc;
            VirtualTexture texV;
            MTexture texM;

            switch (format) {
                case AtlasDataFormat.TexturePacker_Sparrow:
                    xmlDoc = Calc.LoadContentXML(path);
                    XmlElement xmlTextureAtlas = xmlDoc["TextureAtlas"];
                    if (xmlTextureAtlas == null)
                        break;

                    texV = VirtualContent.CreateTexture(Path.Combine(Path.GetDirectoryName(path), xmlTextureAtlas.Attr("imagePath", "")));
                    texM = new MTexture(texV);
                    VTextureToMTextureMap[texV.Name] = texM;
                    atlas.Sources.Add(texV);

                    XmlNodeList xmlSubs = xmlTextureAtlas.GetElementsByTagName("SubTexture");
                    foreach (XmlElement xmlSub in xmlSubs) {
                        string name = xmlSub.Attr("name");
                        Rectangle clipRect = xmlSub.Rect();
                        if (xmlSub.HasAttr("frameX")) {
                            atlas.textures[name] = new MTexture(
                                texM, name, clipRect,
                                new Vector2(-xmlSub.AttrInt("frameX"), -xmlSub.AttrInt("frameY")),
                                xmlSub.AttrInt("frameWidth"), xmlSub.AttrInt("frameHeight")
                            );
                        } else {
                            atlas.textures[name] = new MTexture(texM, name, clipRect);
                        }
                    }
                    break;

                case AtlasDataFormat.CrunchXml:
                    if (!File.Exists(pathFull))
                        break;

                    xmlDoc = Calc.LoadContentXML(path);
                    XmlElement xmlAtlas = xmlDoc["atlas"];

                    foreach (XmlElement xmlAtlasSource in xmlAtlas) {
                        texV = VirtualContent.CreateTexture(Path.Combine(Path.GetDirectoryName(path), xmlAtlasSource.Attr("n", "") + ".png"));
                        texM = new MTexture(texV);
                        VTextureToMTextureMap[texV.Name] = texM;
                        atlas.Sources.Add(texV);
                        foreach (XmlElement xmlSub in xmlAtlasSource) {
                            string name = xmlSub.Attr("n");
                            Rectangle clipRect = new Rectangle(xmlSub.AttrInt("x"), xmlSub.AttrInt("y"), xmlSub.AttrInt("w"), xmlSub.AttrInt("h"));
                            if (xmlSub.HasAttr("fx")) {
                                atlas.textures[name] = new MTexture(
                                    texM, name, clipRect,
                                    new Vector2(-xmlSub.AttrInt("fx"), -xmlSub.AttrInt("fy")),
                                    xmlSub.AttrInt("fw"), xmlSub.AttrInt("fh")
                                );
                            } else {
                                atlas.textures[name] = new MTexture(texM, name, clipRect);
                            }
                        }
                    }
                    break;

                case AtlasDataFormat.CrunchBinary:
                    if (!File.Exists(pathFull))
                        break;

                    using (FileStream stream = File.OpenRead(pathFull))
                    using (BinaryReader reader = new BinaryReader(stream)) {
                        short sources = reader.ReadInt16();
                        for (int i = 0; i < sources; i++) {
                            texV = VirtualContent.CreateTexture(Path.Combine(Path.GetDirectoryName(path), reader.ReadNullTerminatedString() + ".png"));
                            texM = new MTexture(texV);
                            VTextureToMTextureMap[texV.Name] = texM;
                            atlas.Sources.Add(texV);
                            short subs = reader.ReadInt16();
                            for (int j = 0; j < subs; j++) {
                                string name = reader.ReadNullTerminatedString();
                                short clipX = reader.ReadInt16();
                                short clipY = reader.ReadInt16();
                                short clipWidth = reader.ReadInt16();
                                short clipHeight = reader.ReadInt16();
                                short offsX = reader.ReadInt16();
                                short offsY = reader.ReadInt16();
                                short width = reader.ReadInt16();
                                short height = reader.ReadInt16();
                                atlas.textures[name] = new MTexture(
                                    texM, name, new Rectangle(clipX, clipY, clipWidth, clipHeight),
                                    new Vector2(-offsX, -offsY),
                                    width, height
                                );
                            }
                        }
                    }
                    break;

                case AtlasDataFormat.CrunchXmlOrBinary:
                    if (File.Exists(pathFull + ".bin")) {
                        ReadAtlasData(atlas, path + ".bin", AtlasDataFormat.CrunchBinary);
                    } else if (File.Exists(pathFull + ".xml")) {
                        ReadAtlasData(atlas, path + ".xml", AtlasDataFormat.CrunchXml);
                    }
                    return;

                case AtlasDataFormat.CrunchBinaryNoAtlas:
                    if (!File.Exists(pathFull + ".bin"))
                        break;

                    using (FileStream stream = File.OpenRead(pathFull + ".bin"))
                    using (BinaryReader reader = new BinaryReader(stream)) {
                        short sources = reader.ReadInt16();
                        for (int i = 0; i < sources; i++) {
                            string sourcePath = Path.Combine(Path.GetDirectoryName(path), reader.ReadNullTerminatedString());
                            short subs = reader.ReadInt16();
                            for (int j = 0; j < subs; j++) {
                                string name = reader.ReadNullTerminatedString();
                                short unknownA = reader.ReadInt16();
                                short unknownB = reader.ReadInt16();
                                short unknownC = reader.ReadInt16();
                                short unknownD = reader.ReadInt16();
                                short offsX = reader.ReadInt16();
                                short offsY = reader.ReadInt16();
                                short width = reader.ReadInt16();
                                short height = reader.ReadInt16();
                                texV = VirtualContent.CreateTexture(Path.Combine(sourcePath, name + ".png"));
                                atlas.textures[name] = VTextureToMTextureMap[texV.Name] = new MTexture(texV, new Vector2(-offsX, -offsY), width, height);
                                atlas.Sources.Add(texV);
                            }
                        }
                    }
                    break;

                case AtlasDataFormat.Packer:
                    if (!File.Exists(pathFull + ".meta"))
                        break;

                    using (FileStream stream = File.OpenRead(pathFull + ".meta"))
                    using (BinaryReader reader = new BinaryReader(stream)) {
                        reader.ReadInt32(); // ???
                        reader.ReadString(); // ???
                        reader.ReadInt32(); // ???
                        short sources = reader.ReadInt16();
                        for (int i = 0; i < sources; i++) {
                            texV = VirtualContent.CreateTexture(Path.Combine(Path.GetDirectoryName(path), reader.ReadString() + ".data"));
                            texM = new MTexture(texV);
                            VTextureToMTextureMap[texV.Name] = texM;
                            atlas.Sources.Add(texV);
                            short subs = reader.ReadInt16();
                            for (int j = 0; j < subs; j++) {
                                string name = reader.ReadString().Replace('\\', '/');
                                short clipX = reader.ReadInt16();
                                short clipY = reader.ReadInt16();
                                short clipWidth = reader.ReadInt16();
                                short clipHeight = reader.ReadInt16();
                                short offsX = reader.ReadInt16();
                                short offsY = reader.ReadInt16();
                                short width = reader.ReadInt16();
                                short height = reader.ReadInt16();
                                atlas.textures[name] = new MTexture(
                                    texM, name, new Rectangle(clipX, clipY, clipWidth, clipHeight),
                                    new Vector2(-offsX, -offsY),
                                    width, height
                                );
                            }
                        }
                    }
                    break;

                case AtlasDataFormat.PackerNoAtlas:
                    if (!File.Exists(pathFull + ".meta"))
                        break;

                    using (FileStream stream = File.OpenRead(pathFull + ".meta"))
                    using (BinaryReader reader = new BinaryReader(stream)) {
                        reader.ReadInt32();
                        reader.ReadString();
                        reader.ReadInt32();
                        short sources = reader.ReadInt16();
                        for (int i = 0; i < sources; i++) {
                            string sourcePath = Path.Combine(Path.GetDirectoryName(path), reader.ReadString());
                            short subs = reader.ReadInt16();
                            for (int j = 0; j < subs; j++) {
                                string name = reader.ReadString().Replace('\\', '/');
                                short unknownA = reader.ReadInt16();
                                short unknownB = reader.ReadInt16();
                                short unknownC = reader.ReadInt16();
                                short unknownD = reader.ReadInt16();
                                short offsX = reader.ReadInt16();
                                short offsY = reader.ReadInt16();
                                short width = reader.ReadInt16();
                                short height = reader.ReadInt16();
                                texV = VirtualContent.CreateTexture(Path.Combine(sourcePath, name + ".data"));
                                atlas.textures[name] = VTextureToMTextureMap[texV.Name] = new MTexture(texV, new Vector2(-offsX, -offsY), width, height);
                                atlas.Sources.Add(texV);
                            }
                        }
                    }
                    break;

                default:
                    break;
            }
        }

        public static extern Atlas orig_FromAtlas(string path, AtlasDataFormat format);
        public static new Atlas FromAtlas(string path, AtlasDataFormat format) {
            patch_Atlas atlas = (patch_Atlas) orig_FromAtlas(path, format);
            atlas.DataMethod = "FromAtlas";
            atlas.DataPath = path;
            atlas.DataFormat = format;
            Everest.Content.Process(atlas, atlas.DataPath);
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
            return atlas;
        }

        public static extern Atlas orig_FromDirectory(string path);
        public static new Atlas FromDirectory(string path) {
            patch_Atlas atlas = (patch_Atlas) orig_FromDirectory(path);
            atlas.DataMethod = "FromDirectory";
            atlas.DataPath = path;
            Everest.Content.Process(atlas, atlas.DataPath);
            return atlas;
        }

    }
    public static class AtlasExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Get the VTexture -> MTexture map. Note that it only contains the last mapping and is only useful for VTextures which don't contain subtextures.
        /// </summary>
        public static Dictionary<string, MTexture> VTextureToMTextureMap => patch_Atlas.VTextureToMTextureMap;

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
            // Crawl through all child assets.
            if (asset.AssetType == typeof(AssetTypeDirectory)) {
                foreach (ModAsset child in asset.Children)
                    self.Ingest(child);
                return;
            }

            // Forcibly add the mod content to the atlas.
            if (asset.AssetType == typeof(Texture2D)) {
                Logger.Log(LogLevel.Verbose, "Atlas.Ingest", $"{self.GetDataPath()} + {asset.PathMapped}");

                string parentPath = self.GetDataPath();
                if (parentPath.StartsWith(Everest.Content.PathContentOrig))
                    parentPath = parentPath.Substring(Everest.Content.PathContentOrig.Length + 1);
                parentPath = parentPath.Replace('\\', '/');

                bool lq = false;
                string path = asset.PathMapped;

                if (path.StartsWith(parentPath + "LQ/")) {
                    lq = true;
                    path = path.Substring(parentPath.Length + 3);

                } else if (path.StartsWith(parentPath + "/")) {
                    path = path.Substring(parentPath.Length + 1);

                } else {
                    return;
                }

                VirtualTexture vtex = VirtualContentExt.CreateTexture(asset);
                MTexture mtex;
                MTextureMeta meta = asset.GetMeta<MTextureMeta>();
                if (meta != null) {
                    if (meta.Width == 0)
                        meta.Width = vtex.Width;
                    if (meta.Height == 0)
                        meta.Height = vtex.Height;
                }

                Dictionary<string, MTexture> textures = self.GetTextures();
                MTexture existing;
                if (textures.TryGetValue(path, out existing)) {
                    if (lq && !CoreModule.Settings.LQAtlas)
                        return;

                    if (existing.Texture.GetMetadata() == asset)
                        return; // We're the currently active overlay.

                    if (meta != null) {
                        // Apply width and height from existing meta.
                        existing.AddOverride(vtex, new Vector2(meta.X, meta.Y), meta.Width, meta.Height);
                    } else {
                        // Keep width and height from existing instance.
                        existing.AddOverride(vtex, existing.DrawOffset, existing.Width, existing.Height);
                    }

                    mtex = existing;

                } else {
                    if (meta != null) {
                        // Apply width and height from existing meta.
                        mtex = new MTexture(vtex, new Vector2(meta.X, meta.Y), meta.Width, meta.Height);
                    } else {
                        // Apply width and height from replacement texture.
                        mtex = new MTexture(vtex);
                    }
                    mtex.SetAtlasPath(path);
                }

                VTextureToMTextureMap[vtex.Name] = mtex;
                self[path] = mtex;
                if (!self.Sources.Contains(vtex))
                    self.Sources.Add(vtex);
                return;
            }
        }

    }
}
