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
using Ionic.Zip;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Celeste.Mod.Helpers;

namespace Celeste.Mod {
    public class ModAsset {

        /// <summary>
        /// The mod asset source.
        /// </summary>
        public SourceType Source;
        /// <summary>
        /// The type matching the mod asset.
        /// </summary>
        public Type AssetType = null;
        /// <summary>
        /// The original file extension.
        /// </summary>
        public string AssetFormat = null;

        /// <summary>
        /// The virtual path to the asset, matching the mapping path.
        /// </summary>
        public string PathMapped;
        /// <summary>
        /// The path to the source file, or the path to the source entry in an container.
        /// </summary>
        public string PathSource;
        /// <summary>
        /// The path to the source archive.
        /// </summary>
        public string PathArchive;

        /// <summary>
        /// The containing assembly.
        /// </summary>
        public Assembly Assembly;

        /// <summary>
        /// If the asset is a section of a larger file, the asset starting offset.
        /// </summary>
        public long SectionOffset;
        /// <summary>
        /// If the asset is a section of a larger file, the asset length.
        /// </summary>
        public int SectionLength;

        /// <summary>
        /// The "children" assets in f.e. directory type "assets."
        /// </summary>
        public List<ModAsset> Children = new List<ModAsset>();

        /// <summary>
        /// A stream to read the asset data from.
        /// If the asset is a section of a larger file, LimitedStream is used.
        /// </summary>
        public Stream Stream {
            get {
                Stream stream = null;
                if (Source == SourceType.Filesystem) {
                    if (File.Exists(PathSource))
                        stream = File.OpenRead(PathSource);

                } else if (Source == SourceType.Zip) {
                    string file = PathSource.Replace('\\', '/');
                    using (ZipFile zip = new ZipFile(PathArchive)) {
                        foreach (ZipEntry entry in zip.Entries) {
                            if (entry.FileName.Replace('\\', '/') == file) {
                                stream = entry.ExtractStream();
                                break;
                            }
                        }
                    }

                } else if (Source == SourceType.Assembly) {
                    stream = Assembly.GetManifestResourceStream(PathSource);
                }

                if (stream == null || SectionLength == 0) {
                    return stream;
                }
                return new LimitedStream(stream, SectionOffset, SectionLength);
            }
        }

        /// <summary>
        /// The asset contents.
        /// </summary>
        public byte[] Data {
            get {
                using (Stream stream = Stream) {
                    if (stream is MemoryStream) {
                        return ((MemoryStream) stream).GetBuffer();
                    }

                    using (MemoryStream ms = new MemoryStream()) {
                        byte[] buffer = new byte[2048];
                        int read;
                        while (0 < (read = stream.Read(buffer, 0, buffer.Length))) {
                            ms.Write(buffer, 0, read);
                        }
                        return ms.ToArray();
                    }
                }
            }
        }

        public ModAsset() {
            Source = SourceType.None;
        }

        public ModAsset(string file)
            : this(file, 0, 0) {
        }
        public ModAsset(string file, long offset, int length)
            : this() {
            Source = SourceType.Filesystem;
            PathSource = file;
            SectionOffset = offset;
            SectionLength = length;
        }

        public ModAsset(string zip, string file)
            : this(file) {
            Source = SourceType.Zip;
            PathArchive = zip;
            PathSource = file;
        }

        public ModAsset(Assembly assembly, string file)
            : this(file) {
            Source = SourceType.Assembly;
            Assembly = assembly;
        }

        /// <summary>
        /// Deserialize the asset using a deserializer based on the AssetType (f.e. AssetTypeYaml -> YamlDotNet).
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="result">The asset in its deserialized (object) form.</param>
        /// <returns>True if deserializing the asset succeeded, false otherwise.</returns>
        public bool TryDeserialize<T>(out T result) {
            if (AssetType == typeof(AssetTypeYaml)) {
                using (StreamReader reader = new StreamReader(Stream))
                    result = YamlHelper.Deserializer.Deserialize<T>(reader);
                return true;
            }

            result = default(T);
            return false;
        }

        /// <summary>
        /// Deserialize the asset using a deserializer based on the AssetType (f.e. AssetTypeYaml -> YamlDotNet).
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>The asset in its deserialized (object) form or default(T).</returns>
        public T Deserialize<T>() {
            T result;
            TryDeserialize(out result);
            return result;
        }

        /// <summary>
        /// Deserialize this asset's matching .meta asset. Uses TryDeserialize internally.
        /// </summary>
        /// <typeparam name="T">The target meta type.</typeparam>
        /// <param name="meta">The requested meta object.</param>
        /// <returns>True if deserializing the meta asset succeeded, false otherwise.</returns>
        public bool TryGetMeta<T>(out T meta) {
            ModAsset metaAsset;
            if (Everest.Content.TryGet(PathMapped + ".meta", out metaAsset) &&
                metaAsset.TryDeserialize(out meta)
            )
                return true;
            meta = default(T);
            return false;
        }

        /// <summary>
        /// Deserialize this asset's matching .meta asset. Uses TryDeserialize internally.
        /// </summary>
        /// <typeparam name="T">The target meta type.</typeparam>
        /// <returns>The requested meta object or default(T).</returns>
        public T GetMeta<T>() {
            T meta;
            TryGetMeta(out meta);
            return meta;
        }

        public enum SourceType {
            None,
            Filesystem,
            Zip,
            Assembly,
        }

    }
}
