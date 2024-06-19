#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System.Collections;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_OuiFileNaming : OuiFileNaming {

        private int index;
        
        public bool UseKeyboardInput {
            get {
                var settings = Mod.Core.CoreModule.Instance._Settings as Mod.Core.CoreModuleSettings;
                return settings?.UseKeyboardForTextInput ?? false;
            }
        }

        public void OnTextInput(char c) {
            if (c == (char) 13) {
                // Enter - confirm.
                Finish();

            } else if (c == (char) 8) {
                // Backspace - trim.
                if (Name.Length > 0) {
                    Name = Name.Substring(0, Name.Length - 1);
                    Audio.Play(SFX.ui_main_rename_entry_backspace);
                } else {
                    Audio.Play(SFX.ui_main_button_invalid);
                }

            } else if (c == (char) 127) {
                // Delete - currenly not handled.

            } else if (c == ' ') {
                // Space - append.
                if (Name.Length < 12 && Name.Length > 0) {
                    Audio.Play(SFX.ui_main_rename_entry_space);
                    Name += c;
                } else {
                    Audio.Play(SFX.ui_main_button_invalid);
                }

            } else if (!char.IsControl(c)) {
                // Any other character - append.
                if (Name.Length < 12 && ActiveFont.FontSize.Characters.ContainsKey(c)) {
                    Audio.Play(SFX.ui_main_rename_entry_char);
                    Name += c;
                } else {
                    Audio.Play(SFX.ui_main_button_invalid);
                }
            }
        }

        public extern IEnumerator orig_Enter(Oui from);
        public override IEnumerator Enter(Oui from) {
            Engine.Commands.Enabled = false;
            // only subscribe if we're going to use the keyboard
            if (UseKeyboardInput) 
                TextInput.OnInput += OnTextInput;
            return orig_Enter(from);
        }

        public extern IEnumerator orig_Leave(Oui next);
        public override IEnumerator Leave(Oui next) {
            Engine.Commands.Enabled = (Celeste.PlayMode == Celeste.PlayModes.Debug);
            // Non existent unhooks aren't dangerous, and can get us out of weird situations
            TextInput.OnInput -= OnTextInput;
            return orig_Leave(next);
        }

        [MonoModIgnore]
        public extern void ResetDefaultName();

        public extern void orig_Update();
        public override void Update() {
            bool wasFocused = Focused;
            // Only "focus" if we're not using the keyboard for input
            Focused = wasFocused && !UseKeyboardInput;

            // If we aren't focused to kill controller input, still allow the player to Ctrl+S to choose a new name.
            if (Selected && wasFocused && !Focused
                && !string.IsNullOrWhiteSpace(Name) && MInput.Keyboard.Check(Keys.LeftControl) && MInput.Keyboard.Pressed(Keys.S)) {

                ResetDefaultName();
            }

            orig_Update();

            if (wasFocused && !Focused) {
                if (Input.ESC)
                    Cancel();
            }

            Focused = wasFocused;
        }

        [MonoModIgnore]
        private extern void Cancel();

        [MonoModIgnore]
        private extern void Finish();

        [PatchOuiFileNamingRendering]
        public extern void orig_Render();
        public override void Render() {
            int prevIndex = index;
            // Only "focus" if we're not using the keyboard for input
            if (UseKeyboardInput)
                index = -1;

            orig_Render();

            index = prevIndex;
        }

        private extern void orig_DrawOptionText(string text, Vector2 at, Vector2 justify, Vector2 scale, bool selected, bool disabled = false);
        private void DrawOptionText(string text, Vector2 at, Vector2 justify, Vector2 scale, bool selected, bool disabled = false) {
            // Only draw "interactively" if not using the keyboard for input
            if (UseKeyboardInput) {
                selected = false;
                disabled = true;
            }
            orig_DrawOptionText(text, at, justify, scale, selected, disabled);
        }

        private bool _shouldDisplaySwitchAlphabetPrompt() {
            return Japanese && !UseKeyboardInput;
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the file naming rendering to hide the "switch between katakana and hiragana" prompt when the menu is not focused.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchOuiFileNamingRendering))]
    class PatchOuiFileNamingRenderingAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchOuiFileNamingRendering(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_shouldDisplaySwitchAlphabetPrompt = context.Method.DeclaringType.FindMethod("System.Boolean _shouldDisplaySwitchAlphabetPrompt()");

            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(instr => instr.MatchCallvirt("Celeste.OuiFileNaming", "get_Japanese"));
            cursor.Next.OpCode = OpCodes.Call;
            cursor.Next.Operand = m_shouldDisplaySwitchAlphabetPrompt;
        }

    }
}
