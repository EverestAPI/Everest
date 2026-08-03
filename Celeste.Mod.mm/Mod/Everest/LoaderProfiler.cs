using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Celeste.Mod {
    internal static class LoaderProfiler {
        private static readonly ConcurrentDictionary<string, long> ElapsedTicks = new ConcurrentDictionary<string, long>();
        private static readonly ConcurrentDictionary<string, int> Counts = new ConcurrentDictionary<string, int>();
        private static readonly ConcurrentDictionary<string, int> CurrentConcurrency = new ConcurrentDictionary<string, int>();
        private static readonly ConcurrentDictionary<string, int> MaximumConcurrency = new ConcurrentDictionary<string, int>();
        private static readonly ConcurrentQueue<(string Name, long Timestamp)> Events = new ConcurrentQueue<(string, long)>();
        private static long originTimestamp;

        internal static bool Enabled => Environment.GetEnvironmentVariable("EVEREST_LOADER_PROFILE") == "1"
            || File.Exists(Path.Combine(AppContext.BaseDirectory, ".everest-loader-profile"));

        internal static IDisposable Measure(string phase) => Enabled ? new Measurement(phase) : EmptyMeasurement.Instance;
        internal static IDisposable Measure(string phase, string detail) => Enabled
            ? new Measurement($"{phase}/{detail}")
            : EmptyMeasurement.Instance;

        internal static void Reset() {
            if (!Enabled)
                return;
            ElapsedTicks.Clear();
            Counts.Clear();
            CurrentConcurrency.Clear();
            MaximumConcurrency.Clear();
            while (Events.TryDequeue(out _)) { }
            originTimestamp = Stopwatch.GetTimestamp();
        }

        internal static void Mark(string name) {
            if (Enabled)
                Events.Enqueue((name, Stopwatch.GetTimestamp()));
        }

        internal static void Report(long totalMilliseconds) {
            if (!Enabled)
                return;

            Logger.Info("loader-profile", $"phase=total ms={totalMilliseconds}");
            foreach (string phase in ElapsedTicks.Keys.OrderBy(key => key, StringComparer.Ordinal)) {
                double milliseconds = ElapsedTicks[phase] * 1000d / Stopwatch.Frequency;
                Counts.TryGetValue(phase, out int count);
                MaximumConcurrency.TryGetValue(phase, out int maximumConcurrency);
                Logger.Info("loader-profile", $"phase={phase} ms={milliseconds:F3} count={count} maxConcurrency={maximumConcurrency}");
            }
            foreach ((string name, long timestamp) in Events.OrderBy(item => item.Timestamp)) {
                double milliseconds = (timestamp - originTimestamp) * 1000d / Stopwatch.Frequency;
                Logger.Info("loader-profile", $"event={name} at_ms={milliseconds:F3}");
            }
        }

        private sealed class Measurement : IDisposable {
            private readonly string phase;
            private readonly long started = Stopwatch.GetTimestamp();

            internal Measurement(string phase) {
                this.phase = phase;
                int current = CurrentConcurrency.AddOrUpdate(phase, 1, (_, previous) => previous + 1);
                MaximumConcurrency.AddOrUpdate(phase, current, (_, previous) => Math.Max(previous, current));
            }

            public void Dispose() {
                long elapsed = Stopwatch.GetTimestamp() - started;
                ElapsedTicks.AddOrUpdate(phase, elapsed, (_, previous) => previous + elapsed);
                Counts.AddOrUpdate(phase, 1, (_, previous) => previous + 1);
                CurrentConcurrency.AddOrUpdate(phase, 0, (_, previous) => previous - 1);
            }
        }

        private sealed class EmptyMeasurement : IDisposable {
            internal static readonly EmptyMeasurement Instance = new EmptyMeasurement();
            public void Dispose() { }
        }
    }
}
