using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.IO;

namespace Celeste.Mod {
    public abstract class ModAsset {

        /// <summary>
        /// The mod asset's source.
        /// </summary>
        public ModContent Source;

        /// <summary>
        /// The type matching the mod asset.
        /// </summary>
        public Type Type = null;
        /// <summary>
        /// The original file extension.
        /// </summary>
        public string Format = null;

        /// <summary>
        /// The virtual / mapped asset path.
        /// </summary>
        public string PathVirtual;

        /// <summary>
        /// The "children" assets in f.e. directory type "assets."
        /// </summary>
        public List<ModAsset> Children = new List<ModAsset>();

        /// <summary>
        /// A set of all objects affected by this mod asset.
        /// </summary>
        public List<object> Targets = new List<object>();

        /// <summary>
        /// A stream to read the asset data from.
        /// </summary>
        public virtual Stream Stream {
            get {
                Open(out Stream stream, out bool isSection);
                return stream;
            }
        }

        /// <summary>
        /// The contents of the asset.
        /// </summary>
        public virtual byte[] Data {
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

        protected ModAsset(ModContent source) {
            Source = source;
        }

        /// <summary>
        /// Open a stream to read the asset data from.
        /// </summary>
        /// <param name="stream">The resulting stream.</param>
        /// <param name="isSection">Is the stream already a section (SectionOffset and SectionLength)?</param>
        protected abstract void Open(out Stream stream, out bool isSection);

        /// <summary>
        /// Deserialize the asset using a deserializer based on the AssetType (f.e. AssetTypeYaml -> YamlDotNet).
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="result">The asset in its deserialized (object) form.</param>
        /// <returns>True if deserializing the asset succeeded, false otherwise.</returns>
        public bool TryDeserialize<T>(out T result) {
            if (Type == typeof(AssetTypeYaml)) {
                try {
                    using (StreamReader reader = new StreamReader(Stream))
                        result = YamlHelper.Deserializer.Deserialize<T>(reader);
                } catch {
                    result = default;
                    return false;
                }
                return true;
            }

            // TODO: Deserialize AssetTypeXml

            result = default;
            return false;
        }

        /// <summary>
        /// Deserialize the asset using a deserializer based on the AssetType (f.e. AssetTypeYaml -> YamlDotNet).
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>The asset in its deserialized (object) form or default(T).</returns>
        public T Deserialize<T>() {
            TryDeserialize(out T result);
            return result;
        }

        /// <summary>
        /// Deserialize this asset's matching .meta asset. Uses TryDeserialize internally.
        /// </summary>
        /// <typeparam name="T">The target meta type.</typeparam>
        /// <param name="meta">The requested meta object.</param>
        /// <returns>True if deserializing the meta asset succeeded, false otherwise.</returns>
        public bool TryGetMeta<T>(out T meta) {
            if (Everest.Content.TryGet(PathVirtual + ".meta", out ModAsset metaAsset) &&
                metaAsset.TryDeserialize(out meta)
            )
                return true;
            meta = default;
            return false;
        }

        /// <summary>
        /// Deserialize this asset's matching .meta asset. Uses TryDeserialize internally.
        /// </summary>
        /// <typeparam name="T">The target meta type.</typeparam>
        /// <returns>The requested meta object or default(T).</returns>
        public T GetMeta<T>() {
            TryGetMeta(out T meta);
            return meta;
        }

        /// <summary>
        /// Cache the file and return a cached path.
        /// </summary>
        /// <returns>The cached file path.</returns>
        public virtual string GetCachedPath() {
            EverestModuleMetadata mod = Source?.Mod;
            if (mod == null)
                throw new NullReferenceException("Cannot cache mod-less assets");

            string path = Path.Combine(Everest.Loader.PathCache, mod.Name, PathVirtual.Replace('/', Path.DirectorySeparatorChar));
            string pathSum = path + ".sum";

            string sum = mod.Hash.ToHexadecimalString();

            if (File.Exists(path)) {
                if (File.Exists(pathSum) && File.ReadAllText(pathSum) == sum)
                    return path;
                File.Delete(pathSum);
                File.Delete(path);
            }

            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            WriteCache(path);
            File.WriteAllText(pathSum, sum);

            return path;
        }

        protected virtual void WriteCache(string path) {
            using (Stream streamIn = Stream)
            using (Stream streamOut = File.OpenWrite(path))
                streamIn.CopyTo(streamOut);
        }

    }

    public abstract class ModAsset<T> : ModAsset where T : ModContent {
        public new T Source => base.Source as T;
        protected ModAsset(T source)
            : base(source) {
        }
    }

    public sealed class ModAssetBranch : ModAsset {
        public ModAssetBranch()
            : base(null) {
        }

        protected override void Open(out Stream stream, out bool isSection) {
            throw new InvalidOperationException();
        }
    }

    public class FileSystemModAsset : ModAsset<FileSystemModContent> {
        /// <summary>
        /// The path to the source file.
        /// </summary>
        public readonly string Path;

        public FileSystemModAsset(FileSystemModContent source, string path)
            : base(source) {
            Path = path;
        }

        protected override void Open(out Stream stream, out bool isSection) {
            if (!File.Exists(Path)) {
                stream = null;
                isSection = false;
                return;
            }

            stream = File.OpenRead(Path);
            isSection = false;
        }

        public override string GetCachedPath() {
            return Path;
        }
    }

    public class MapBinsInModsModAsset : ModAsset<MapBinsInModsModContent> {
        /// <summary>
        /// The path to the source file.
        /// </summary>
        public readonly string Path;

        public MapBinsInModsModAsset(MapBinsInModsModContent source, string path)
            : base(source) {
            Path = path;
        }

        protected override void Open(out Stream stream, out bool isSection) {
            if (!File.Exists(Path)) {
                stream = null;
                isSection = false;
                return;
            }

            stream = File.OpenRead(Path);
            isSection = false;
        }

        public override string GetCachedPath() {
            return Path;
        }
    }

    public class AssemblyModAsset : ModAsset<AssemblyModContent> {
        /// <summary>
        /// The name of the resource in the assembly.
        /// </summary>
        public readonly string ResourceName;

        public AssemblyModAsset(AssemblyModContent source, string resourceName)
            : base(source) {
            ResourceName = resourceName;
        }

        protected override void Open(out Stream stream, out bool isSection) {
            stream = Source.Assembly.GetManifestResourceStream(ResourceName);
            isSection = false;
        }
    }

    public class ZipModAsset : ModAsset<ZipModContent> {
        /// <summary>
        /// The path to the source file inside the archive.
        /// </summary>
        public readonly string Path;

        /// <summary>
        /// The entry for the source file inside the archive.
        /// </summary>
        public readonly ZipEntry Entry;

        public ZipModAsset(ZipModContent source, string path)
            : base(source) {
            Path = path = path.Replace('\\', '/');

            foreach (ZipEntry entry in source.Zip.Entries) {
                if (entry.FileName.Replace('\\', '/') == path) {
                    Entry = entry;
                    break;
                }
            }
        }

        public ZipModAsset(ZipModContent source, ZipEntry entry)
            : base(source) {
            Path = entry.FileName.Replace('\\', '/');
            Entry = entry;
        }

        protected override void Open(out Stream stream, out bool isSection) {
            string path = Path;

            ZipEntry found = Entry;
            if (found == null) {
                foreach (ZipEntry entry in Source.Zip.Entries) {
                    if (entry.FileName.Replace('\\', '/') == path) {
                        stream = entry.ExtractStream();
                        isSection = false;
                        return;
                    }
                }
            }

            if (found == null)
                throw new KeyNotFoundException($"{GetType().Name} {Path} not found in archive {Source.Path}");

            stream = Entry.ExtractStream();
            isSection = false;
        }
    }
}
