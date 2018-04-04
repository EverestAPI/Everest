#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste {
    class patch_OverworldLoader : OverworldLoader {

        public patch_OverworldLoader(Overworld.StartMode startMode, HiresSnow snow = null)
            : base(startMode, snow) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        private object _LoadThread_Lock;
        private Thread _LoadThread_MainThread;

        public extern void orig_Begin();
        public override void Begin() {
            _LoadThread_Lock = new object();
            _LoadThread_MainThread = Thread.CurrentThread;

            orig_Begin();

            if (CoreModule.Settings.NonThreadedGL) {
                LoadThread();
            }
        }

        private extern void orig_LoadThread();
        private void LoadThread() {
            lock (_LoadThread_Lock) {
                if (CoreModule.Settings.NonThreadedGL && Thread.CurrentThread != _LoadThread_MainThread)
                    return;

                orig_LoadThread();
            }
        }

    }
}
