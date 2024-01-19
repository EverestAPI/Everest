using Celeste.Mod;
using Celeste.Mod.Helpers;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Celeste {
    static class patch_RunThread {

        private static List<Thread> threads = new List<Thread>();
        private static List<DateTime> threadTimes = new List<DateTime>();
        private static List<string> threadInfos = new List<string>();

        [ThreadStatic]
        public static WeakReference<Thread> Current;

        [MonoModReplace]
        public static void Start(Action method, string name, bool highPriority = false) {
            Thread thread = new Thread(() => RunThreadWithLogging(method)) {
                Name = name,
                IsBackground = true,
                Priority = highPriority ? ThreadPriority.Highest : ThreadPriority.Normal
            };
            lock (threads) {
                threads.Add(thread);
                threadTimes.Add(DateTime.UtcNow);
                threadInfos.Add(string.Format("Name: {0}\nAction: {1}\nStarter:\n{2}", name, method?.Method?.ToString() ?? method.ToString(), new StackTrace(1)));
            }
            Current = new WeakReference<Thread>(thread);
            thread.Start();
        }

        [MonoModReplace]
        private static void RunThreadWithLogging(Action method) {
            try {
                method();

            } catch (ThreadAbortException e) {
                Logger.Warn("RunThread", $"Background thread {Thread.CurrentThread?.Name ?? "???"} aborted");
                Logger.LogDetailed(e);

            } catch (Exception e) {
                ErrorLog.Write(e);
                ErrorLog.Open();
                Engine.Instance.Exit();

            } finally {
                lock (threads) {
                    int index = threads.IndexOf(Thread.CurrentThread);
                    if (index != -1) {
                        threads.RemoveAt(index);
                        threadTimes.RemoveAt(index);
                        threadInfos.RemoveAt(0);
                    }
                }
            }
        }

        [MonoModReplace]
        public static void WaitAll() {
            while (threads.Count > 0) {
                Thread thread;
                DateTime start;
                DateTime? timeout = null;
                lock (threads) {
                    thread = threads[0];
                    start = threadTimes[0];
                }

                while (thread.IsAlive) {
                    // Some mods (mis)use RunThread.Start for long-living background threads
                    // which prevent the game from shutting down.
                    if ((DateTime.UtcNow - start).TotalSeconds >= 10) {
                        // Even if a background thread lives on for way too long, give it some
                        // time to recognize that the game is shutting down before discarding it.
                        if (timeout == null)
                            timeout = DateTime.UtcNow + TimeSpan.FromSeconds(5);
                        if ((DateTime.UtcNow - timeout.Value).Ticks >= 0) {
                            lock (threads) {
                                Logger.Verbose("RunThread.WaitAll", $"Background thread taking too long, discarding it.\n{threadInfos[0]}");
                                threads.RemoveAt(0);
                                threadTimes.RemoveAt(0);
                                threadInfos.RemoveAt(0);
                                break;
                            }
                        }
                    }

                    try {
                        Engine.Instance.GraphicsDevice.Present();
                    } catch {
                        // Celeste 1.3.3.x does this but is this really needed?
                    }
                }
            }
        }

    }
}
