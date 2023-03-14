using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using static Celeste.Mod.Helpers.Win32;

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

            Type t_TextInputExt = typeof(Keyboard).Assembly.GetType("Microsoft.Xna.Framework.Input.TextInputEXT");
            EventInfo e_TextInput = t_TextInputExt?.GetEvent("TextInput");
            if (e_TextInput != null) {
                // FNA offers Microsoft.Xna.Framework.Input.TextInputEXT,
                // public static event Action<char> TextInput;
                e_TextInput.AddEventHandler(null, new Action<char>(ReceiveTextInput).CastDelegate(e_TextInput.EventHandlerType));

                // Some platforms like Linux/Wayland may require calling TextInputEXT.StartTextInput to receive events for TextInputEXT.TextInput
                MethodInfo m_StartTextInput = t_TextInputExt?.GetMethod("StartTextInput", new Type[] { } );
                m_StartTextInput?.Invoke(t_TextInputExt, null);

                // SDL2 offers SDL_GetClipboardText and SDL_SetClipboardText
                Type t_SDL2 = typeof(Keyboard).Assembly.GetType("SDL2.SDL");
                _GetClipboardText = t_SDL2.GetMethod("SDL_GetClipboardText").CreateDelegate(typeof(Func<string>)) as Func<string>;
                Func<string, int> setClipboardText = t_SDL2.GetMethod("SDL_SetClipboardText").CreateDelegate(typeof(Func<string, int>)) as Func<string, int>;
                _SetClipboardText = value => setClipboardText(value);

            } else {
                // XNA requires WinForms message hooks.
                FormsHook = new XNAFormsHook(game.Window.Handle, (ref Message msg) => {
                    if (msg.Msg != 0x0102)
                        return;
                    ReceiveTextInput((char) msg.WParam);
                });

                // WinForms offers Clipboard.GetText and SetText
                Type t_Clipboard = Assembly.Load("System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089").GetType("System.Windows.Forms.Clipboard");
                Func<string> getClipboardText = t_Clipboard.GetMethod("GetText", new Type[] { } ).CreateDelegate(typeof(Func<string>)) as Func<string>;
                _GetClipboardText = () => STAThreadHelper.Get(getClipboardText).GetResult();
                Action<string> setClipboardText = t_Clipboard.GetMethod("SetText", new Type[] { typeof(string) }).CreateDelegate(typeof(Action<string>)) as Action<string>;
                _SetClipboardText = (value) => STAThreadHelper.Get(() => {
                    try {
                        setClipboardText(string.IsNullOrEmpty(value) ? "\0" : value);
                    } catch (ExternalException e) {
                        Logger.Log(LogLevel.Warn, "TextInputs", "Failed to set the clipboard");
                        Logger.LogDetailed(e);
                    }
                    return value;
                }).GetResult();
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
        public static event Action<char> OnInput;

        private static Func<string> _GetClipboardText;
        public static string GetClipboardText() => _GetClipboardText?.Invoke();

        private static Action<string> _SetClipboardText;
        public static void SetClipboardText(string value) => _SetClipboardText?.Invoke(value);

    }
}
