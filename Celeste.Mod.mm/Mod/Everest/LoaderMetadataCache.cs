using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Celeste.Mod {
    internal static class LoaderMetadataCache {
        internal sealed class ZipMetadata {
            public long Length { get; set; }
            public long LastWriteUtcTicks { get; set; }
            public string Yaml { get; set; }
            public List<string> IgnoreLines { get; set; }
            public List<string> AssemblyEntries { get; set; }
            public bool HasBothMetadataFiles { get; set; }

            [JsonIgnore]
            private HashSet<string> assemblyEntrySet;

            internal bool ContainsAssemblyEntry(string path) {
                assemblyEntrySet ??= new HashSet<string>(AssemblyEntries ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                return assemblyEntrySet.Contains(path.Replace('\\', '/'));
            }
        }

        private sealed class CacheDocument {
            public int Version { get; set; } = 1;
            public Dictionary<string, ZipMetadata> Entries { get; set; } = new Dictionary<string, ZipMetadata>(StringComparer.OrdinalIgnoreCase);
        }

        private static readonly object Sync = new object();
        private static CacheDocument document = new CacheDocument();
        private static string cachePath;
        private static bool dirty;

        internal static bool Enabled => Environment.GetEnvironmentVariable("EVEREST_METADATA_CACHE") != "0"
            && !File.Exists(Path.Combine(AppContext.BaseDirectory, ".everest-disable-metadata-cache"));

        internal static void Initialize(string pathCache) {
            cachePath = Path.Combine(pathCache, "everest-metadata-cache-v1.json");
            document = new CacheDocument();
            dirty = false;
            if (!Enabled || !File.Exists(cachePath))
                return;
            try {
                document = JsonConvert.DeserializeObject<CacheDocument>(File.ReadAllText(cachePath)) ?? new CacheDocument();
                if (document.Version != 1 || document.Entries == null)
                    document = new CacheDocument();
            } catch (Exception e) {
                Logger.Warn("loader", $"Ignoring invalid metadata cache: {e.Message}");
                document = new CacheDocument();
            }
        }

        internal static void Prefetch(IEnumerable<string> archives) {
            if (!Enabled)
                return;
            string[] misses = archives.Where(path => !TryGet(path, out _)).ToArray();
            if (misses.Length == 0)
                return;
            using (LoaderProfiler.Measure("metadata-prefetch")) {
                Parallel.ForEach(misses, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8) }, archive => {
                    try {
                        Store(archive, ReadArchive(archive));
                    } catch (Exception e) {
                        Logger.Warn("loader", $"Failed prefetching metadata from {archive}: {e.Message}");
                    }
                });
            }
        }

        internal static bool TryGet(string archive, out ZipMetadata metadata) {
            metadata = null;
            if (!Enabled)
                return false;
            FileInfo info = new FileInfo(archive);
            lock (Sync) {
                if (!document.Entries.TryGetValue(archive, out ZipMetadata cached)
                    || cached.Length != info.Length
                    || cached.LastWriteUtcTicks != info.LastWriteTimeUtc.Ticks
                    || cached.AssemblyEntries == null)
                    return false;
                metadata = cached;
                return true;
            }
        }

        internal static bool TryContainsAssemblyEntry(string archive, string path, out bool contains) {
            contains = false;
            if (!TryGet(archive, out ZipMetadata metadata))
                return false;
            contains = metadata.ContainsAssemblyEntry(path);
            return true;
        }

        internal static ZipMetadata ReadArchive(string archive) {
            FileInfo info = new FileInfo(archive);
            ZipMetadata result = new ZipMetadata {
                Length = info.Length,
                LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks,
                AssemblyEntries = new List<string>()
            };
            bool foundMetadata = false;
            using ZipArchive zip = ZipFile.OpenRead(archive);
            foreach (ZipArchiveEntry entry in zip.Entries) {
                if (entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    || entry.FullName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                    result.AssemblyEntries.Add(entry.FullName.Replace('\\', '/'));

                if (entry.FullName is "everest.yaml" or "everest.yml") {
                    if (foundMetadata) {
                        result.HasBothMetadataFiles = true;
                        continue;
                    }
                    using Stream stream = entry.Open();
                    using StreamReader reader = new StreamReader(stream);
                    result.Yaml = reader.ReadToEnd();
                    foundMetadata = true;
                } else if (entry.FullName == ".everestignore") {
                    using Stream stream = entry.Open();
                    using StreamReader reader = new StreamReader(stream);
                    result.IgnoreLines = new List<string>();
                    while (!reader.EndOfStream)
                        result.IgnoreLines.Add(reader.ReadLine());
                }
            }
            return result;
        }

        internal static void Store(string archive, ZipMetadata metadata) {
            if (!Enabled)
                return;
            lock (Sync) {
                document.Entries[archive] = metadata;
                dirty = true;
            }
        }

        internal static void Flush() {
            if (!Enabled || !dirty)
                return;
            try {
                string json;
                lock (Sync)
                    json = JsonConvert.SerializeObject(document, Formatting.None);
                string temporary = cachePath + ".tmp";
                File.WriteAllText(temporary, json);
                File.Move(temporary, cachePath, true);
                dirty = false;
            } catch (Exception e) {
                Logger.Warn("loader", $"Failed saving metadata cache: {e.Message}");
            }
        }
    }
}
