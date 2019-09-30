using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using Monocle;
using MonoMod.Utils;
using MonoMod.InlineRT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Celeste.Mod {
    public static class QueuedTaskHelper {

        private static readonly Dictionary<object, object> Map = new Dictionary<object, object>();
        private static readonly Dictionary<object, Stopwatch> Timers = new Dictionary<object, Stopwatch>();

        private const double Delay = 0.5D;

        public static Task Do(object key, Action a) {
            lock (Map) {
                if (Map.TryGetValue(key, out object queued)) {
                    Timers[key].Restart();
                    return (Task) queued;
                }

                Stopwatch timer = Stopwatch.StartNew();
                Timers[key] = timer;
                Task t = new Func<Task>(async () => {
                    do {
                        await Task.Delay(TimeSpan.FromSeconds(Delay - timer.Elapsed.TotalSeconds));
                    } while (timer.Elapsed.TotalSeconds < Delay);

                    Map.Remove(key);
                    timer.Stop();

                    a?.Invoke();
                })();

                Map[key] = t;
                return t;
            }
        }

        public static Task<T> Get<T>(object key, Func<T> f) {
            lock (Map) {
                if (Map.TryGetValue(key, out object queued)) {
                    Timers[key].Restart();
                    return (Task<T>) queued;
                }

                Stopwatch timer = Stopwatch.StartNew();
                Timers[key] = timer;
                Task<T> t = new Func<Task<T>>(async () => {
                    do {
                        await Task.Delay(TimeSpan.FromSeconds(Delay - timer.Elapsed.TotalSeconds));
                    } while (timer.Elapsed.TotalSeconds < Delay);

                    Map.Remove(key);
                    timer.Stop();

                    return f != null ? f.Invoke() : default(T);
                })();

                Map[key] = t;
                return t;
            }
        }

    }
}
