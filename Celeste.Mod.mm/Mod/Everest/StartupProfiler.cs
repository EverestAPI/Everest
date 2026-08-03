using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Celeste.Mod {
    internal static class StartupProfiler {
        private static readonly ConcurrentDictionary<string, long> ElapsedTicks = new ConcurrentDictionary<string, long>();
        private static readonly ConcurrentDictionary<string, int> Counts = new ConcurrentDictionary<string, int>();
        private static readonly ConcurrentQueue<(string Name, long Timestamp)> Events = new ConcurrentQueue<(string, long)>();
        private static long originTimestamp;
        private static int initialized;
        private static int reported;

        internal static bool Enabled => Environment.GetEnvironmentVariable("EVEREST_STARTUP_PROFILE") == "1"
            || File.Exists(Path.Combine(AppContext.BaseDirectory, ".everest-startup-profile"));

        internal static void Initialize() {
            if (!Enabled || Interlocked.Exchange(ref initialized, 1) != 0)
                return;
            originTimestamp = Stopwatch.GetTimestamp();
            Events.Enqueue(("process-entry", originTimestamp));
        }

        internal static IDisposable Measure(string phase) {
            EnsureInitialized();
            return Enabled ? new Measurement(phase) : EmptyMeasurement.Instance;
        }

        internal static IDisposable Measure(string phase, string detail) {
            EnsureInitialized();
            return Enabled ? new Measurement($"{phase}/{detail}") : EmptyMeasurement.Instance;
        }

        internal static void Mark(string name) {
            EnsureInitialized();
            if (Enabled)
                Events.Enqueue((name, Stopwatch.GetTimestamp()));
        }

        internal static void Report() {
            if (!Enabled || Interlocked.Exchange(ref reported, 1) != 0)
                return;

            long finished = Stopwatch.GetTimestamp();
            double totalMilliseconds = (finished - originTimestamp) * 1000d / Stopwatch.Frequency;
            Logger.Info("startup-profile", $"total ms={totalMilliseconds:F3}");
            foreach (string phase in ElapsedTicks.Keys.OrderBy(key => key, StringComparer.Ordinal)) {
                double milliseconds = ElapsedTicks[phase] * 1000d / Stopwatch.Frequency;
                Counts.TryGetValue(phase, out int count);
                Logger.Info("startup-profile", $"phase={phase} ms={milliseconds:F3} count={count}");
            }
            foreach ((string name, long timestamp) in Events.OrderBy(item => item.Timestamp)) {
                double milliseconds = (timestamp - originTimestamp) * 1000d / Stopwatch.Frequency;
                Logger.Info("startup-profile", $"event={name} at_ms={milliseconds:F3}");
            }
            Logger.Info("startup-profile", "complete");
        }

        private static void EnsureInitialized() {
            if (Enabled && Volatile.Read(ref initialized) == 0)
                Initialize();
        }

        private sealed class Measurement : IDisposable {
            private readonly string phase;
            private readonly long started = Stopwatch.GetTimestamp();
            private int disposed;

            internal Measurement(string phase) {
                this.phase = phase;
            }

            public void Dispose() {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                    return;
                long elapsed = Stopwatch.GetTimestamp() - started;
                ElapsedTicks.AddOrUpdate(phase, elapsed, (_, previous) => previous + elapsed);
                Counts.AddOrUpdate(phase, 1, (_, previous) => previous + 1);
            }
        }

        private sealed class EmptyMeasurement : IDisposable {
            internal static readonly EmptyMeasurement Instance = new EmptyMeasurement();
            public void Dispose() { }
        }
    }
}
