﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SDL2;
using System;

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

            // Subscribing is useless as long as we don't call `StartTextInput` so its ok for this to be here
            TextInputEXT.TextInput += ReceiveTextInput;
            CheckTextStatus(); // Required check to handle pre-init subscriptions
        }

        internal static void Shutdown() {
            if (!Initialized)
                return;

            if (TextInputEXT.IsTextInputActive())
                TextInputEXT.StopTextInput();
            TextInputEXT.TextInput -= ReceiveTextInput;
        }

        internal static void ReceiveTextInput(char c) {
            // Invoke our own event handler.
            _OnInput?.Invoke(c);
        }

        private static event Action<char> _OnInput;

        /// <summary>
        /// Invoked whenever text input occurs, including some "input action" characters.
        /// This event is in charge of managing `StartTextInput` and `StopTextInput` calls.
        /// Consequently its use should be restricted to when text input from the keyboard is needed.
        /// Note that the aforementioned FNA calls will bring up virtual keyboard (if available, Steam Deck is an
        /// example), so it's the modder's job to make sure it does appear and disappear correctly.
        /// Take a look at the FNA TextInputExt documentation for more info: https://github.com/FNA-XNA/FNA/wiki/5:-FNA-Extensions#textinputext
        /// </summary>
        public static event Action<char> OnInput {
            add {
                _OnInput += value;
                CheckTextStatus();
            }
            remove {
                _OnInput -= value;
                CheckTextStatus();
            }
        }

        private static void CheckTextStatus() {
            if (!Initialized) return; // No text updates before this is initialized
            if (_OnInput != null && _OnInput.GetInvocationList().Length != 0 && !TextInputEXT.IsTextInputActive()) {
                TextInputEXT.StartTextInput();
            } else if ((_OnInput == null || _OnInput.GetInvocationList().Length == 0) && TextInputEXT.IsTextInputActive()) {
                TextInputEXT.StopTextInput();
            }

            // Warn the modder if there's ever multiple subscriptions, because chances are that they misused the event
            if (_OnInput?.GetInvocationList().Length > 1) {
                Logger.Log(LogLevel.Warn, "TextInput", 
                    "Simultaneous text input subscriptions detected, is this a bug? See TextInput.OnInput for proper usage");
            }
        }

        public static string GetClipboardText() => SDL.SDL_GetClipboardText();
        public static void SetClipboardText(string value) => SDL.SDL_SetClipboardText(value);

    }
}
