using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Celeste.Mod {
    /// <summary>
    /// Class containing a text input event for your mods.
    /// Uses FNA's TextInputEXT if available, falling back to a WinForms hook.
    /// </summary>
    public static class TextInput {

        public static bool Initialized { get; private set; }

        private static XNAFormsHook FormsHook;

        internal static void Initialize(Game game) {
            if (Initialized)
                return;
            Initialized = true;

            // FNA offers Microsoft.Xna.Framework.Input.TextInputEXT,
            // public static event Action<char> TextInput;
            Type ext = typeof(Keyboard).Assembly.GetType("Microsoft.Xna.Framework.Input.TextInputEXT");
            EventInfo extEvent = ext?.GetEvent("TextInput");
            if (extEvent != null) {
                extEvent.AddEventHandler(null, new Action<char>(ReceiveTextInput).CastDelegate(extEvent.EventHandlerType));

            } else {
                // XNA requires WinForms message hooks.
                FormsHook = new XNAFormsHook(game.Window.Handle, (ref Win32.Message msg) => {
                    if (msg.Msg != 0x0102)
                        return;
                    ReceiveTextInput((char) msg.WParam);
                });
            }
        }

        internal static void ReceiveTextInput(char c) {
            // Invoke our own event handler.
            OnInput?.Invoke(c);
        }

        /// <summary>
        /// Invoked whenever text input occurs, including some "input action" characters.
        /// Take a look at the FNA TextInputExt documentation for more info: https://github.com/FNA-XNA/FNA/wiki/5:-FNA-Extensions#textinputext
        /// </summary>
        public static Action<char> OnInput;

    }
}
