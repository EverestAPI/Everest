using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SDL2;
using System;
using System.Reflection;

namespace Celeste.Mod {
    /// <summary>
    /// Class containing a text input event for your mods.
    /// Uses FNA's TextInputEXT if available, falling back to a WinForms hook.
    /// </summary>
    public static class TextInput {

        public static bool Initialized { get; private set; }

        internal static void Initialize(Game game) {
            if (Initialized)
                return;
            Initialized = true;

            TextInputEXT.TextInput += ReceiveTextInput;
            TextInputEXT.StartTextInput();
        }

        internal static void Shutdown() {
            if (!Initialized)
                return;

            TextInputEXT.StopTextInput();
            TextInputEXT.TextInput -= ReceiveTextInput;
        }

        internal static void ReceiveTextInput(char c) {
            // Invoke our own event handler.
            OnInput?.Invoke(c);
        }

        /// <summary>
        /// Invoked whenever text input occurs, including some "input action" characters.
        /// Take a look at the FNA TextInputExt documentation for more info: https://github.com/FNA-XNA/FNA/wiki/5:-FNA-Extensions#textinputext
        /// </summary>
        public static event Action<char> OnInput;

        public static string GetClipboardText() => SDL.SDL_GetClipboardText();
        public static void SetClipboardText(string value) => SDL.SDL_SetClipboardText(value);

    }
}
