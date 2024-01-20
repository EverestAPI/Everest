using MonoMod;
using SDL2;

namespace Microsoft.Xna.Framework {
    
    [GameDependencyPatch("FNA")]
    public class patch_Game {
         public GameWindow Window { get; private set; }
        private extern void orig_BeforeLoop();
        private void BeforeLoop() {
            orig_BeforeLoop();

            // On certain setups the splash causes the celeste window to in background rather than straight to the top
            // This should fix that on those plaforms
            // SDL_HINT_FORCE_RAISEWINDOW is required solely for Windows, ignored on other systems
            SDL.SDL_SetHint("SDL_HINT_FORCE_RAISEWINDOW", "true");
            SDL.SDL_RaiseWindow(Window.Handle);
        } 

    }
}