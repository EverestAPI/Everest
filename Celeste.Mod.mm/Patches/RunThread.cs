#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Celeste.Mod;
using FMOD;
using FMOD.Studio;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

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
