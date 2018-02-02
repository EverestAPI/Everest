#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using MonoMod;
using System;

namespace Monocle {
    class patch_Engine : Engine {

        public patch_Engine(int width, int height, int windowWidth, int windowHeight, string windowTitle, bool fullscreen)
            : base(width, height, windowWidth, windowHeight, windowTitle, fullscreen) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModReplace]
        public new void RunWithLogging() {
            try {
                base.Run();
            } catch (Exception e) {
                e.LogDetailed();
                ErrorLog.Write(e);
                ErrorLog.Open();
            }
        }

    }
}
