using MonoMod;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

namespace Microsoft.Xna.Framework.Input {
    [GameDependencyPatch("FNA")]
    static class patch_Keyboard {

        // FNA's keyboard input driver is rather jank (1 frame latency for IMEs
        // even when disabled, not registering all keys, etc.)
        //
        // As such replace it with our own one based on XNA's code, which is
        // simply wrapping the Win32 GetKeyboardState API

        private static bool UseNewInputDriver = false;

        [MonoModConstructor] // Work around MonoMod jank
        static patch_Keyboard() {
            if (Environment.GetEnvironmentVariable("EVEREST_NEW_KEYBOARD_INPUT") is string cfgEnv)
                if (int.TryParse(cfgEnv, out int cfgEnvVal))
                    UseNewInputDriver = cfgEnvVal == 1;

            orig_ctor_Keyboard();
        }
        private static extern void orig_ctor_Keyboard();

        public static extern KeyboardState orig_GetState();
        public static KeyboardState GetState() {
            if (!UseNewInputDriver || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return orig_GetState();

            // Obtain the current keyboard state
            Span<byte> state = stackalloc byte[256];
            unsafe {
                fixed (byte* statePtr = state) {
                    if (!GetKeyboardState(statePtr))
                        throw new Win32Exception("Failed to get Win32 keyboard state");
                }
            }

            // Construct a KeyboardState object
            KeyboardState kbState = new KeyboardState();
            for (int i = 0; i < 256; i++) {
                // > When the function returns, each member of the array pointed
                // > to by the lpKeyState parameter contains status data for a
                // > virtual key. If the high-order bit is 1, the key is down;
                // > otherwise, it is up.
                if ((state[i] & 0x80) != 0)
                    // We love unsafe magic :peaceline:
                    Unsafe.As<KeyboardState, patch_KeyboardState>(ref kbState).SetKey((Keys) i);
            }
            return kbState;
        }

        [MonoModReplace]
        public static KeyboardState GetState(PlayerIndex playerIndex) => GetState();

        
        [MonoModLinkTo("Microsoft.Xna.Framework.Input.KeyboardState", "System.Void InternalSetKey(Microsoft.Xna.Framework.Input.Keys)")]
        [MonoModRemove]
        internal static extern void KeyboardState_InternalSetKey(ref KeyboardState state, Keys key);

        [SupportedOSPlatform("windows")]
        [DllImport("user32.dll", SetLastError = true)]
        private static extern unsafe bool GetKeyboardState(byte* lpKeyState);

    }

    [GameDependencyPatch("FNA")]
    struct patch_KeyboardState {

        // The FNA-internal method is private - we need to expose it internally

        [MonoModIgnore]
        private extern void InternalSetKey(Keys key);

        internal void SetKey(Keys key) => InternalSetKey(key);

    }
}

#pragma warning restore CS0626 // Method, operator, or accessor is marked external and has no attributes on it