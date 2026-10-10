using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Celeste.Mod {
    /// <summary>
    /// Persistent, stat-validated file checksum cache used by the startup loader.
    /// </summary>
    internal static class LoaderChecksumCache {
        private const int HashHexLength = sizeof(ulong) * 2;

        private sealed class ChecksumEntry {
            public long Length { get; set; }
            public long LastWriteUtcTicks { get; set; }
            public string Hash { get; set; }
        }

        private readonly record struct FileFingerprint(string Path, long Length, long LastWriteUtcTicks);

        private static readonly object Sync = new object();
        private static Dictionary<string, ChecksumEntry> entries =
            new Dictionary<string, ChecksumEntry>(StringComparer.OrdinalIgnoreCase);
        private static string cachePath;
        private static bool dirty;

        internal static void Initialize(string pathCache) {
            lock (Sync) {
                cachePath = Path.Combine(pathCache, "everest-checksums-v1.json");
                entries = new Dictionary<string, ChecksumEntry>(StringComparer.OrdinalIgnoreCase);
                dirty = false;

                if (!File.Exists(cachePath))
                    return;

                try {
                    Dictionary<string, ChecksumEntry> loaded =
                        JsonConvert.DeserializeObject<Dictionary<string, ChecksumEntry>>(File.ReadAllText(cachePath));
                    if (loaded != null)
                        entries = new Dictionary<string, ChecksumEntry>(loaded, StringComparer.OrdinalIgnoreCase);
                } catch (Exception e) {
                    Logger.Warn("loader", $"Ignoring invalid checksum cache: {e.Message}");
                }
            }
        }

        internal static byte[] GetChecksum(string path) {
            lock (Sync) {
                if (cachePath == null)
                    return Calculate(path);

                for (int attempt = 0; attempt < 3; attempt++) {
                    FileFingerprint fingerprint = GetFingerprint(path);
                    if (entries.TryGetValue(fingerprint.Path, out ChecksumEntry entry)
                        && entry.Length == fingerprint.Length
                        && entry.LastWriteUtcTicks == fingerprint.LastWriteUtcTicks) {
                        try {
                            if (entry.Hash?.Length == HashHexLength)
                                return Convert.FromHexString(entry.Hash);
                        } catch (FormatException) { }
                        entries.Remove(fingerprint.Path);
                        dirty = true;
                    }

                    byte[] hash;
                    try {
                        // Do not cache a hash while another process has the file open for writing.
                        hash = Calculate(fingerprint.Path, FileShare.Read);
                    } catch (IOException) {
                        continue;
                    }
                    if (GetFingerprint(fingerprint.Path).Equals(fingerprint)) {
                        entries[fingerprint.Path] = new ChecksumEntry {
                            Length = fingerprint.Length,
                            LastWriteUtcTicks = fingerprint.LastWriteUtcTicks,
                            Hash = hash.ToHexadecimalString()
                        };
                        dirty = true;
                        return hash;
                    }
                }

                return Calculate(path);
            }
        }

        internal static void Flush() {
            lock (Sync) {
                if (cachePath == null || !dirty)
                    return;

                string temporary = $"{cachePath}.{Environment.ProcessId}.tmp";
                try {
                    File.WriteAllText(temporary, JsonConvert.SerializeObject(entries, Formatting.None));
                    File.Move(temporary, cachePath, true);
                    dirty = false;
                } catch (Exception e) {
                    Logger.Warn("loader", $"Failed saving checksum cache: {e.Message}");
                    try {
                        File.Delete(temporary);
                    } catch { }
                }
            }
        }

        private static FileFingerprint GetFingerprint(string path) {
            FileInfo info = new FileInfo(path);
            return new FileFingerprint(info.FullName, info.Length, info.LastWriteTimeUtc.Ticks);
        }

        private static byte[] Calculate(string path) {
            return Calculate(path, FileShare.ReadWrite | FileShare.Delete);
        }

        private static byte[] Calculate(string path, FileShare share) {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, share,
                bufferSize: 128 * 1024, FileOptions.SequentialScan);
            return Everest.ComputeHash(stream);
        }
    }
}
