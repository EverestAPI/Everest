using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static class QueuedTaskHelper {

        private static readonly ConcurrentDictionary<object, object> Map = new ConcurrentDictionary<object, object>();
        private static readonly ConcurrentDictionary<object, Stopwatch> Timers = new ConcurrentDictionary<object, Stopwatch>();

        public static readonly double DefaultDelay = 0.5D;

        public static void Cancel(object key) {
            if (Timers.TryRemove(key, out Stopwatch timer)) {
                timer.Stop();
                if (!Map.TryRemove(key, out _))
                    throw new Exception("Queued task cancellation failed!");
            }
        }

        public static Task Do(object key, Action a)
            => Do(key, DefaultDelay, a);
        public static Task Do(object key, double delay, Action a) {
            object queued = Map.GetOrAdd(key, key => {
                Stopwatch timer = Stopwatch.StartNew();
                Timers[key] = timer;
                return new Func<Task>(async () => {
                    do {
                        await Task.Delay(TimeSpan.FromSeconds(delay - timer.Elapsed.TotalSeconds));
                    } while (timer.Elapsed.TotalSeconds < delay);

                    if (!timer.IsRunning)
                        return;

                    Cancel(key);

                    a?.Invoke();
                })();
            });

            Timers[key].Restart();
            return (Task) queued;
        }

        public static Task<T> Get<T>(object key, Func<T> f)
            => Get(key, DefaultDelay, f);
        public static Task<T> Get<T>(object key, double delay, Func<T> f) {
            object queued = Map.GetOrAdd(key, key => {
                Stopwatch timer = Stopwatch.StartNew();
                Timers[key] = timer;
                return new Func<Task<T>>(async () => {
                    do {
                        await Task.Delay(TimeSpan.FromSeconds(delay - timer.Elapsed.TotalSeconds));
                    } while (timer.Elapsed.TotalSeconds < delay);

                    Cancel(key);

                    return f != null ? f.Invoke() : default;
                })();
            });

            Timers[key].Restart();
            return (Task<T>) queued;
        }

    }
}
