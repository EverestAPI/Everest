using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;
using SDL2;
using System;

namespace Microsoft.Xna.Framework {
    [GameDependencyPatch("FNA")]
    static class patch_SDL2_FNAPlatform {

        [MonoModIgnore]
        [PatchFNASDL2ApplyWindowChanges]
        public static extern void ApplyWindowChanges(IntPtr window, int clientWidth, int clientHeight, bool wantsFullscreen, string screenDeviceName, ref string resultDeviceName);

        public static bool ShouldUseExclusiveFullscreen() => false;

        private static void ApplyExclusiveFullscreen(IntPtr window, int displayIndex) {
            SDL.SDL_DisplayMode mode;
            SDL.SDL_GetCurrentDisplayMode(displayIndex, out mode);
            SDL.SDL_SetWindowSize(window, mode.w, mode.h);
            SDL.SDL_SetWindowFullscreen(window, (uint) SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN);
        }

    }
}

namespace MonoMod {
    /// <summary>
    /// Patches SDL2_FNAPlatform.ApplyWindowChanges to use exclusive fullscreen when requested
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchFNASDL2ApplyWindowChanges))]

    class PatchFNASDL2ApplyWindowChangesAttribute : Attribute { }
        static partial class MonoModRules {

        public static void PatchFNASDL2ApplyWindowChanges(MethodDefinition method, CustomAttribute attrib) {
            new ILContext(method).Invoke(ctx => {
                ILCursor cursor = new ILCursor(ctx);

                cursor.GotoNext(MoveType.After, i => i.MatchLdarg(3), i => i.MatchBrfalse(out _));
                cursor.Prev.MatchBrfalse(out ILLabel endLabel);

                // Find the displayIndex local var
                ILCursor varFindCursor = new ILCursor(ctx);
                varFindCursor.GotoNext(i => i.MatchLdloc(out _), i => i.MatchLdloca(out _), i => i.MatchCall("SDL2.SDL", "SDL_GetCurrentDisplayMode"));
                varFindCursor.Next.MatchLdloc(out int displayIndexLocIdx);

                // Determine if we should use exclusive fullscreen
                ILLabel borderlessFullscreenLabel = cursor.DefineLabel();
                cursor.Emit(OpCodes.Call, method.DeclaringType.FindMethod("System.Boolean ShouldUseExclusiveFullscreen()"));
                cursor.Emit(OpCodes.Brfalse_S, borderlessFullscreenLabel);

                // Apply exclusive fullscreen
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldloc, displayIndexLocIdx);
                cursor.Emit(OpCodes.Call, method.DeclaringType.FindMethod("System.Void ApplyExclusiveFullscreen(System.IntPtr,System.Int32)"));
                cursor.Emit(OpCodes.Br, endLabel);

                cursor.MarkLabel(borderlessFullscreenLabel);
            });
        }

    }
}