using Celeste.Mod;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Threading;

namespace Celeste {
    static class patch_RunThread {

        [ThreadStatic]
        public static WeakReference<Thread> Current;

        [MonoModReplace]
        public static void Start(Action method, string name, bool highPriority = false) {
            Thread thread = new Thread(() => RunThreadWithLogging(method)) {
                Name = name,
                IsBackground = true,
                Priority = highPriority ? ThreadPriority.Highest : ThreadPriority.Normal
            };
            Current = new WeakReference<Thread>(thread);
            thread.Start();
        }

        [MonoModReplace]
        private static void RunThreadWithLogging(Action method) {
            try {
                method();

            } catch (ThreadAbortException e) {
                Logger.Log(LogLevel.Warn, "RunThread", $"Background thread {Thread.CurrentThread?.Name ?? "???"} aborted");
                e.LogDetailed();

            } catch (Exception e) {
                ErrorLog.Write(e);
                ErrorLog.Open();
                Engine.Instance.Exit();
            }
        }

    }
}
