#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;
using SDL2;
using System;

namespace Microsoft.Xna.Framework {
    
    [GameDependencyPatch("FNA")]
    public class patch_Game {
        public GameWindow Window { get; private set; }
        private extern void orig_BeforeLoop();
        private void BeforeLoop() {
            orig_BeforeLoop();

            // This env variable will be set to "1" by everest if the user has not set it and the splash has ran
            if (Environment.GetEnvironmentVariable("EVEREST_SKIP_REQUEST_FOCUS_AFTER_SPLASH") == "1") return;
            // On certain setups the splash causes the celeste window to in background rather than straight to the top
            // This should fix that on those plaforms
            // SDL_HINT_FORCE_RAISEWINDOW is required solely for Windows, ignored on other systems
            SDL.SDL_SetHint("SDL_HINT_FORCE_RAISEWINDOW", "true");
            SDL.SDL_RaiseWindow(Window.Handle);
        } 

    }
}