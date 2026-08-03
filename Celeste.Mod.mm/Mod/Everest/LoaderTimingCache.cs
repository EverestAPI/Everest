using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Celeste.Mod {
    /// <summary>
    /// Small profile-guided cache used by the startup DAG scheduler. It stores an
    /// exponentially weighted wall-time estimate per mod, then uses those estimates
    /// to start expensive critical-path nodes before short leaf work on later boots.
    /// </summary>
    internal static class LoaderTimingCache {
        private sealed class TimingEntry {
            public string VersionString { get; set; }
            public double AverageMilliseconds { get; set; }
            public int Samples { get; set; }
        }

        private sealed class CacheDocument {
            public int Version { get; set; } = 1;
            public Dictionary<string, TimingEntry> Entries { get; set; } = new Dictionary<string, TimingEntry>(StringComparer.Ordinal);
        }

        private static readonly object Sync = new object();
        private static CacheDocument document = new CacheDocument();
        private static string cachePath;
        private static bool dirty;

        internal static bool Enabled => Environment.GetEnvironmentVariable("EVEREST_LOADER_PGO") != "0"
            && !File.Exists(Path.Combine(AppContext.BaseDirectory, ".everest-disable-loader-pgo"));

        internal static void Initialize(string pathCache) {
            cachePath = Path.Combine(pathCache, "everest-loader-timings-v1.json");
            document = new CacheDocument();
            dirty = false;
            if (!Enabled || !File.Exists(cachePath))
                return;

            try {
                document = JsonConvert.DeserializeObject<CacheDocument>(File.ReadAllText(cachePath)) ?? new CacheDocument();
                if (document.Version != 1 || document.Entries == null)
                    document = new CacheDocument();
            } catch (Exception e) {
                Logger.Warn("loader", $"Ignoring invalid loader timing cache: {e.Message}");
                document = new CacheDocument();
            }
        }

        internal static double GetEstimate(EverestModuleMetadata meta, out int samples) {
            samples = 0;
            if (!Enabled || meta == null)
                return DefaultEstimate(meta);

            lock (Sync) {
                if (document.Entries.TryGetValue(GetKey(meta), out TimingEntry entry)
                    && entry.VersionString == meta.VersionString
                    && entry.AverageMilliseconds > 0d) {
                    samples = entry.Samples;
                    return entry.AverageMilliseconds;
                }
            }
            return DefaultEstimate(meta);
        }

        internal static void Record(EverestModuleMetadata meta, double milliseconds) {
            if (!Enabled || meta == null || !double.IsFinite(milliseconds) || milliseconds <= 0d)
                return;

            milliseconds = Math.Clamp(milliseconds, 0.05d, 300_000d);
            lock (Sync) {
                string key = GetKey(meta);
                if (!document.Entries.TryGetValue(key, out TimingEntry entry)
                    || entry.VersionString != meta.VersionString) {
                    document.Entries[key] = new TimingEntry {
                        VersionString = meta.VersionString,
                        AverageMilliseconds = milliseconds,
                        Samples = 1
                    };
                } else {
                    // Learn quickly for the first few starts, then retain enough history to
                    // avoid one antivirus / relink outlier completely reshuffling the DAG.
                    double alpha = entry.Samples < 4 ? 1d / (entry.Samples + 1d) : 0.25d;
                    entry.AverageMilliseconds += (milliseconds - entry.AverageMilliseconds) * alpha;
                    entry.Samples = Math.Min(entry.Samples + 1, 1000);
                }
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
                Logger.Warn("loader", $"Failed saving loader timing cache: {e.Message}");
            }
        }

        private static string GetKey(EverestModuleMetadata meta) =>
            $"{meta.Name}\n{meta.PathArchive ?? meta.PathDirectory ?? meta.DLL ?? string.Empty}";

        private static double DefaultEstimate(EverestModuleMetadata meta) =>
            string.IsNullOrEmpty(meta?.DLL) ? 2d : 25d;
    }
}
