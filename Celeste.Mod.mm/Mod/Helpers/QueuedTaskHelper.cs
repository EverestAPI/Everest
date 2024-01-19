using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static class QueuedTaskHelper {

        // Make sure to lock Timers and update both of those at the same time before unlocking!
        private static readonly Dictionary<object, object> Map = new Dictionary<object, object>();
        private static readonly Dictionary<object, Stopwatch> Timers = new Dictionary<object, Stopwatch>();

        public static readonly double DefaultDelay = 0.5D;

        public static void Cancel(object key) {
            lock (Timers) {
                if (Timers.Remove(key, out Stopwatch timer)) {
                    timer.Stop();
                    if (!Map.Remove(key))
                        throw new Exception("Queued task cancellation failed!");
                }
            }
        }

        public static Task Do(object key, Action a)
            => Do(key, DefaultDelay, a);
        public static Task Do(object key, double delay, Action a) {
            lock (Timers) {
                if (!Timers.TryGetValue(key, out Stopwatch timer)) {
                    timer = Stopwatch.StartNew();
                    Timers.Add(key, timer);
                }

                if (!Map.TryGetValue(key, out object queued)) {
                    queued = new Func<Task>(async () => {
                        await Task.Yield();

                        do {
                            await Task.Delay(TimeSpan.FromSeconds(delay - timer.Elapsed.TotalSeconds));
                        } while (timer.Elapsed.TotalSeconds < delay);

                        if (!timer.IsRunning)
                            return;

                        Cancel(key);

                        a?.Invoke();
                    })();

                    Map.Add(key, queued);
                }

                timer.Restart();
                return (Task) queued;
            }
        }

        public static Task<T> Get<T>(object key, Func<T> f)
            => Get(key, DefaultDelay, f);
        public static Task<T> Get<T>(object key, double delay, Func<T> f) {
            lock (Timers) {
                if (!Timers.TryGetValue(key, out Stopwatch timer)) {
                    timer = Stopwatch.StartNew();
                    Timers.Add(key, timer);
                }

                if (!Map.TryGetValue(key, out object queued)) {
                    queued = new Func<Task<T>>(async () => {
                        await Task.Yield();

                        do {
                            await Task.Delay(TimeSpan.FromSeconds(delay - timer.Elapsed.TotalSeconds));
                        } while (timer.Elapsed.TotalSeconds < delay);

                        Cancel(key);

                        return f != null ? f.Invoke() : default;
                    })();

                    Map.Add(key, queued);
                }

                timer.Restart();
                return (Task<T>) queued;
            }
        }

    }
}
