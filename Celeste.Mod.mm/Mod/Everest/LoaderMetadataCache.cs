using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Celeste.Mod {
    internal static class LoaderMetadataCache {
        private static readonly bool Enabled = Environment.GetEnvironmentVariable("EVEREST_METADATA_CACHE") != "0";

        internal sealed class ZipMetadata {
            public long Length { get; set; }
            public long LastWriteUtcTicks { get; set; }
            public string Yaml { get; set; }
            public List<string> IgnoreLines { get; set; }
            public List<string> AssemblyEntries { get; set; }
            public bool HasBothMetadataFiles { get; set; }
        }

        private sealed class CacheDocument {
            public int Version { get; set; } = 1;
            public Dictionary<string, ZipMetadata> Entries { get; set; } =
                new Dictionary<string, ZipMetadata>(StringComparer.OrdinalIgnoreCase);
        }

        private static readonly object Sync = new object();
        private static CacheDocument document = new CacheDocument();
        private static string cachePath;
        private static bool dirty;

        internal static void Initialize(string pathCache) {
            cachePath = Path.Combine(pathCache, "everest-metadata-cache-v1.json");
            document = new CacheDocument();
            dirty = false;
            if (!Enabled || !File.Exists(cachePath))
                return;

            try {
                document = JsonConvert.DeserializeObject<CacheDocument>(File.ReadAllText(cachePath)) ?? new CacheDocument();
                if (document.Version != 1 || document.Entries == null) {
                    document = new CacheDocument();
                } else {
                    // Newtonsoft doesn't preserve comparers, so restore case-insensitive lookups.
                    document.Entries = new Dictionary<string, ZipMetadata>(document.Entries, StringComparer.OrdinalIgnoreCase);
                }
            } catch (Exception e) {
                Logger.Warn("loader", $"Ignoring invalid metadata cache: {e.Message}");
                document = new CacheDocument();
            }
        }

        internal static bool TryGet(string archive, out ZipMetadata metadata) {
            metadata = null;
            if (!Enabled || string.IsNullOrEmpty(archive))
                return false;

            FileInfo info = new FileInfo(archive);
            lock (Sync) {
                if (!document.Entries.TryGetValue(info.FullName, out ZipMetadata cached)
                    || cached == null
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
            string normalized = path.Replace('\\', '/');
            contains = metadata.AssemblyEntries.Any(s => s.Equals(normalized, StringComparison.OrdinalIgnoreCase));
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
                string name = entry.FullName;
                if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                    result.AssemblyEntries.Add(name.Replace('\\', '/'));

                if (name is "everest.yaml" or "everest.yml") {
                    if (foundMetadata) {
                        result.HasBothMetadataFiles = true;
                        continue;
                    }
                    using Stream stream = entry.Open();
                    using StreamReader reader = new StreamReader(stream);
                    result.Yaml = reader.ReadToEnd();
                    foundMetadata = true;
                } else if (name == ".everestignore") {
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
                document.Entries[new FileInfo(archive).FullName] = metadata;
                dirty = true;
            }
        }

        internal static void Flush() {
            if (!Enabled)
                return;

            lock (Sync) {
                if (!dirty)
                    return;

                string temporary = cachePath + ".tmp";
                try {
                    File.WriteAllText(temporary, JsonConvert.SerializeObject(document, Formatting.None));
                    File.Move(temporary, cachePath, true);
                    dirty = false;
                } catch (Exception e) {
                    Logger.Warn("loader", $"Failed saving metadata cache: {e.Message}");
                    try {
                        File.Delete(temporary);
                    } catch { }
                }
            }
        }
    }
}
