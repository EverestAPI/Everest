using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Celeste.Mod {
    /// <summary>
    /// Persistent, stat-validated file checksum cache used by the startup loader.
    /// A warm boot can validate an unchanged ZIP or DLL with metadata instead of
    /// reading the entire file again. Concurrent requests for the same fingerprint
    /// share a single hash calculation.
    /// </summary>
    internal static class LoaderChecksumCache {
        private sealed class ChecksumEntry {
            public long Length { get; set; }
            public long LastWriteUtcTicks { get; set; }
            public string Hash { get; set; }

            [JsonIgnore]
            public byte[] RuntimeHash { get; set; }
        }

        private sealed class CacheDocument {
            public int Version { get; set; } = 1;
            public Dictionary<string, ChecksumEntry> Entries { get; set; } =
                new Dictionary<string, ChecksumEntry>(StringComparer.OrdinalIgnoreCase);
        }

        private readonly record struct FileFingerprint(string Path, long Length, long LastWriteUtcTicks);

        private static readonly object Sync = new object();
        private static readonly ConcurrentDictionary<FileFingerprint, Lazy<byte[]>> InFlight = new();
        private static CacheDocument document = new CacheDocument();
        private static string cachePath;
        private static bool initialized;
        private static bool dirty;

        internal static bool Enabled => Environment.GetEnvironmentVariable("EVEREST_CHECKSUM_CACHE") != "0"
            && !File.Exists(Path.Combine(AppContext.BaseDirectory, ".everest-disable-checksum-cache"));

        internal static void Initialize(string pathCache) {
            cachePath = Path.Combine(pathCache, "everest-checksums-v1.json");
            document = new CacheDocument();
            InFlight.Clear();
            dirty = false;
            initialized = true;

            if (!Enabled || !File.Exists(cachePath))
                return;

            try {
                using (LoaderProfiler.Measure("checksum-cache-read"))
                    document = JsonConvert.DeserializeObject<CacheDocument>(File.ReadAllText(cachePath)) ?? new CacheDocument();
                if (document.Version != 1 || document.Entries == null)
                    document = new CacheDocument();
            } catch (Exception e) {
                Logger.Warn("loader", $"Ignoring invalid checksum cache: {e.Message}");
                document = new CacheDocument();
            }
        }

        internal static byte[] GetChecksum(string path) {
            if (!Enabled || !initialized)
                return Calculate(path);

            FileInfo info = new FileInfo(path);
            string fullPath = info.FullName;
            long length = info.Length;
            long lastWriteUtcTicks = info.LastWriteTimeUtc.Ticks;

            lock (Sync) {
                if (document.Entries.TryGetValue(fullPath, out ChecksumEntry entry)
                    && entry.Length == length
                    && entry.LastWriteUtcTicks == lastWriteUtcTicks
                    && !string.IsNullOrEmpty(entry.Hash)) {
                    using (LoaderProfiler.Measure("checksum-cache-hit"))
                        return entry.RuntimeHash ??= Convert.FromHexString(entry.Hash);
                }
            }

            FileFingerprint fingerprint = new FileFingerprint(fullPath, length, lastWriteUtcTicks);
            Lazy<byte[]> calculation = InFlight.GetOrAdd(fingerprint, key => new Lazy<byte[]>(() => {
                byte[] hash;
                using (LoaderProfiler.Measure("checksum-cache-miss-hash", Path.GetFileName(key.Path)))
                    hash = Calculate(key.Path);

                lock (Sync) {
                    document.Entries[key.Path] = new ChecksumEntry {
                        Length = key.Length,
                        LastWriteUtcTicks = key.LastWriteUtcTicks,
                        Hash = hash.ToHexadecimalString(),
                        RuntimeHash = hash
                    };
                    dirty = true;
                }
                return hash;
            }, LazyThreadSafetyMode.ExecutionAndPublication));

            try {
                return calculation.Value;
            } finally {
                InFlight.TryRemove(new KeyValuePair<FileFingerprint, Lazy<byte[]>>(fingerprint, calculation));
            }
        }

        internal static void Flush() {
            if (!Enabled || !initialized || !dirty)
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
                Logger.Warn("loader", $"Failed saving checksum cache: {e.Message}");
            }
        }

        private static byte[] Calculate(string path) {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 128 * 1024, FileOptions.SequentialScan);
            return Everest.ComputeHash(stream);
        }
    }
}
