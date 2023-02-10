#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Logger = Celeste.Mod.Logger;

namespace Monocle {
    class patch_Atlas : Atlas {

        // We're effectively in Atlas, but still need to "expose" private fields to our mod.
        private Dictionary<string, MTexture> textures;
        /// <summary>
        /// The internal string-MTexture dictionary.
        /// </summary>
        public Dictionary<string, MTexture> Textures => textures;
        private Dictionary<string, string> links = new Dictionary<string, string>();
        private Dictionary<string, List<MTexture>> orderedTexturesCache;
        private Stack<MTexture> FallbackStack;

        public MTexture DefaultFallback;

        public string DataMethod;
        public string DataPath;
        public string RelativeDataPath;
        public string[] DataPaths;
        public AtlasDataFormat? DataFormat;

        [MonoModReplace]
        private static void ReadAtlasData(Atlas _atlas, string path, AtlasDataFormat format) {
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
                    texM.SetAtlas(atlas);
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
                        texM.SetAtlas(atlas);
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
                            texM.SetAtlas(atlas);
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
                                texM = atlas.textures[name] = new MTexture(texV, new Vector2(-offsX, -offsY), width, height);
                                texM.SetAtlas(atlas);
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
                        reader.ReadInt32(); // version
                        reader.ReadString(); // args
                        reader.ReadInt32(); // hash
                        short sources = reader.ReadInt16();
                        for (int i = 0; i < sources; i++) {
                            texV = VirtualContent.CreateTexture(Path.Combine(Path.GetDirectoryName(path), reader.ReadString() + ".data"));
                            texM = new MTexture(texV);
                            texM.SetAtlas(atlas);
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
                        if (stream.Position < stream.Length && reader.ReadString() == "LINKS") {
                            short count = reader.ReadInt16();
                            for (int i = 0; i < count; i++) {
                                string key = reader.ReadString();
                                string value = reader.ReadString();
                                atlas.links.Add(key, value);
                            }
                        }
                    }
                    break;

                case AtlasDataFormat.PackerNoAtlas:
                    if (!File.Exists(pathFull + ".meta"))
                        break;

                    using (FileStream stream = File.OpenRead(pathFull + ".meta"))
                    using (BinaryReader reader = new BinaryReader(stream)) {
                        reader.ReadInt32(); // version
                        reader.ReadString(); // args
                        reader.ReadInt32(); // hash
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
                                texM = atlas.textures[name] = new MTexture(texV, new Vector2(-offsX, -offsY), width, height);
                                texM.SetAtlas(atlas);
                                texM.AtlasPath = name;
                                atlas.Sources.Add(texV);
                            }
                        }
                        if (stream.Position < stream.Length && reader.ReadString() == "LINKS") {
                            short count = reader.ReadInt16();
                            for (int i = 0; i < count; i++) {
                                string key = reader.ReadString();
                                string value = reader.ReadString();
                                atlas.links.Add(key, value);
                            }
                        }
                    }
                    break;

                default:
                    break;
            }
        }

        /// <origdoc/>
        public static extern Atlas orig_FromAtlas(string path, AtlasDataFormat format);
        /// <inheritdoc cref="Atlas.FromAtlas(string, AtlasDataFormat)"/>
        public static new Atlas FromAtlas(string path, AtlasDataFormat format) {
            patch_Atlas atlas = (patch_Atlas) orig_FromAtlas(path, format);
            atlas.DataMethod = "FromAtlas";
            atlas.DataPath = path;
            atlas.DataFormat = format;
            atlas.FixDataPath();
            Everest.Content.ProcessLoad(atlas, atlas.DataPath);
            return atlas;
        }

        /// <origdoc/>
        public static extern Atlas orig_FromMultiAtlas(string rootPath, string[] dataPath, AtlasDataFormat format);
        /// <inheritdoc cref="Atlas.FromMultiAtlas(string, string[], AtlasDataFormat)"/>
        public static new Atlas FromMultiAtlas(string rootPath, string[] dataPath, AtlasDataFormat format) {
            patch_Atlas atlas = (patch_Atlas) orig_FromMultiAtlas(rootPath, dataPath, format);
            atlas.DataMethod = "FromMultiAtlas";
            atlas.DataPath = rootPath;
            atlas.DataPaths = dataPath;
            atlas.DataFormat = format;
            atlas.FixDataPath();
            Everest.Content.ProcessLoad(atlas, atlas.DataPath);
            return atlas;
        }

        /// <origdoc/>
        public static extern Atlas orig_FromMultiAtlas(string rootPath, string filename, AtlasDataFormat format);
        /// <inheritdoc cref="Atlas.FromMultiAtlas(string, string, AtlasDataFormat)"/>
        public static new Atlas FromMultiAtlas(string rootPath, string filename, AtlasDataFormat format) {
            patch_Atlas atlas = (patch_Atlas) orig_FromMultiAtlas(rootPath, filename, format);
            atlas.DataMethod = "FromMultiAtlas";
            atlas.DataPath = rootPath;
            atlas.DataPaths = new string[] { filename };
            atlas.DataFormat = format;
            atlas.FixDataPath();
            Everest.Content.ProcessLoad(atlas, atlas.DataPath);
            return atlas;
        }

        /// <origdoc/>
        public static extern Atlas orig_FromDirectory(string path);
        /// <inheritdoc cref="Atlas.FromDirectory(string)"/>
        public static new Atlas FromDirectory(string path) {
            patch_Atlas atlas = (patch_Atlas) orig_FromDirectory(path);
            atlas.DataMethod = "FromDirectory";
            atlas.DataPath = path;
            atlas.FixDataPath();
            Everest.Content.ProcessLoad(atlas, atlas.DataPath);
            return atlas;
        }

        private void FixDataPath() {
            if (DataPath == null)
                return;

            string path = DataPath;
            if (path.StartsWith(Everest.Content.PathContentOrig))
                path = path.Substring(Everest.Content.PathContentOrig.Length + 1);
            path = path.Replace('\\', '/');
            RelativeDataPath = path + "/";
        }

        public void ResetCaches() {
            if (orderedTexturesCache.Count > 0)
                orderedTexturesCache = new Dictionary<string, List<MTexture>>();
        }

        public MTexture GetFallback() {
            if (FallbackStack != null && FallbackStack.Count > 0)
                return FallbackStack.Peek();

            if (DefaultFallback != null || textures.TryGetValue("__fallback", out DefaultFallback))
                return DefaultFallback;

            return null;
        }

        public void PushFallback(MTexture fallback) {
            if (FallbackStack == null)
                FallbackStack = new Stack<MTexture>();
            FallbackStack.Push(fallback);
        }

        public MTexture PopFallback() {
            return FallbackStack.Pop();
        }

        /// <summary>
        /// Feed the given ModAsset into the atlas.
        /// </summary>
        public void Ingest(ModAsset asset) {
            if (asset == null)
                return;

            // Crawl through all child assets.
            if (asset.Type == typeof(AssetTypeDirectory)) {
                lock (asset.Children) {
                    foreach (ModAsset child in asset.Children)
                        Ingest(child);
                }
                return;
            }

            // Forcibly add the mod content to the atlas.
            if (asset.Type != typeof(Texture2D))
                return;

            string path = asset.PathVirtual;

            if (!path.StartsWith(RelativeDataPath))
                return;
            path = path.Substring(RelativeDataPath.Length);

            if (textures.TryGetValue(path, out MTexture mtex)) {
                Logger.Log(LogLevel.Verbose, "Atlas.Ingest", $"{Path.GetFileName(DataPath)} + ({asset.Source?.Name ?? "???"}) {path}");
                mtex.SetOverride(asset);
                this[path] = mtex;
                return;
            }

            VirtualTexture vtex;
            try {
                vtex = VirtualContentExt.CreateTexture(asset);
            } catch {
                // The game is going to crash from this. Log the offending texture to make debugging easier.
                Logger.Log(LogLevel.Error, "Atlas.Ingest", $"Error while loading texture {path} ({asset.Source?.Name ?? "???"}) into atlas {Path.GetFileName(DataPath)}");
                throw;
            }
            MTextureMeta meta = asset.GetMeta<MTextureMeta>();
            if (meta != null) {
                // Apply width and height from meta.
                if (meta.Width == 0)
                    meta.Width = vtex.Width;
                if (meta.Height == 0)
                    meta.Height = vtex.Height;
                mtex = new MTexture(vtex, new Vector2(meta.X, meta.Y), meta.Width, meta.Height);
            } else {
                // Apply width and height from replacement texture.
                mtex = new MTexture(vtex);
            }
            mtex.AtlasPath = path;
            mtex.SetAtlas(this);
            mtex.SetOverride(asset);
            this[path] = mtex;
            Sources.Add(vtex);
        }

        public bool HasAtlasSubtextures(string key) {
            return HasAtlasSubtexturesAt(key, 0);
        }

        public bool HasAtlasSubtexturesAt(string key, int index) {
            if (orderedTexturesCache.TryGetValue(key, out List<MTexture> list)) {
                return index >= 0 && index < list.Count;
            } else {
                return GetAtlasSubtextureFromAtlasAt(key, index) != null;
            }
        }

        // log missing subtextures when getting an animation (for example, decals)
        public extern List<MTexture> orig_GetAtlasSubtextures(string key);
        public new List<MTexture> GetAtlasSubtextures(string key) {
            PushFallback(null);
            List<MTexture> result = orig_GetAtlasSubtextures(key);
            PopFallback();
            if (result == null || result.Count == 0) {
                Logger.Log(LogLevel.Warn, "Atlas", $"Requested atlas subtextures but none were found: {key}");
                MTexture fallback = GetFallback();
                if (fallback != null)
                    return new List<MTexture>() { fallback };
            }
            return result;
        }

        [MonoModIgnore]
        private extern MTexture GetAtlasSubtextureFromAtlasAt(string key, int index);

        [MonoModReplace]
        public new MTexture GetAtlasSubtexturesAt(string key, int index) {
            if (orderedTexturesCache.TryGetValue(key, out List<MTexture> list)) {
                if (index < 0 || index >= list.Count) {
                    Logger.Log(LogLevel.Warn, "Atlas", $"Requested atlas subtexture that falls out of range: {key} {index}");
                    return GetFallback();
                }
                return list[index];
            }

            MTexture result = GetAtlasSubtextureFromAtlasAt(key, index);
            if (result == null) {
                MTexture fallback = GetFallback();
                if (fallback != null) {
                    // SpriteData and other places use GetAtlasSubtextureAt to check if textures exist.
                    // Logging this verbosely when no fallback exists doesn't make sense in those cases.
                    Logger.Log(LogLevel.Warn, "Atlas", $"Requested atlas subtexture that does not exist: {key} {index}");
                    return fallback;
                }
            }
            return result;
        }

        // log missing texture when getting one by ID (for example, tilesets)
        public new MTexture this[string id] {
            [MonoModReplace]
            get {
                if (!textures.TryGetValue(id, out MTexture result)) {
                    Logger.Log(LogLevel.Warn, "Atlas", $"Requested texture that does not exist: {id}");
                    return GetFallback();
                }
                return result;
            }

            // we don't want to modify the setter, but want it to exist in the patch class so that we can call it from within our patches.
            [MonoModIgnore]
            set { }
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

        public static void ResetCaches(this Atlas self)
            => ((patch_Atlas) self).ResetCaches();

        /// <summary>
        /// Feed the given ModAsset into the atlas.
        /// </summary>
        public static void Ingest(this Atlas self, ModAsset asset)
            => ((patch_Atlas) self).Ingest(asset);

    }
}
