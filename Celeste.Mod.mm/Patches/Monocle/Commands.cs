#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Monocle {
    class patch_Commands : Commands {

        // We're effectively in Commands, but still need to "expose" private fields to our mod.
        private bool canOpen;
        private KeyboardState currentState;
        private bool underscore;
        private float underscoreCounter;
        private Keys? repeatKey;
        private float repeatCounter;
        private string currentText = "";
        private List<patch_Line> drawCommands;

        private int mouseScroll;
        private int cursorScale;
        private int charIndex;

        private static readonly Lazy<bool> celesteTASInstalled = new Lazy<bool>(() => Everest.Modules.Any(module => module.Metadata?.Name == "CelesteTAS"));

        [MonoModReplace] // Don't create orig_ method.
        internal void UpdateClosed() {
            if (!canOpen) {
                canOpen = true;
            // Original code only checks OemTillde and Oem8, leaving QWERTZ users in the dark...
            } else if (MInput.Keyboard.Pressed(Keys.OemTilde, Keys.Oem8, Keys.OemPeriod)) {
                Open = true;
                currentState = Keyboard.GetState();
            }

            // Execute F-key actions.
            for (int i = 0; i < FunctionKeyActions.Length; i++)
                if (MInput.Keyboard.Pressed(Keys.F1 + i))
                    ExecuteFunctionKeyAction(i);
        }

        internal void DrawCursor(Vector2 position, int scale, Color color) {
            for (int i = -scale / 2; i <= scale / 2; i++) {
                Draw.Line(position.X - 4f * scale, position.Y + i, position.X - 2f * scale, position.Y + i, color);
                Draw.Line(position.X + 2f * scale - 1f, position.Y + i, position.X + 4f * scale - 1f, position.Y + i, color);
                Draw.Line(position.X + i, position.Y - 4f * scale + 1f, position.X + i, position.Y - 2f * scale + 1f, color);
                Draw.Line(position.X + i, position.Y + 2f * scale, position.X + i, position.Y + 4f * scale, color);
            }
            Draw.Line(position.X - 3f, position.Y, position.X + 2f, position.Y, color);
            Draw.Line(position.X, position.Y - 2f, position.X, position.Y + 3f, color);
        }

        [MonoModReplace]
        internal void Render() {
            int viewWidth = Engine.ViewWidth;
            int viewHeight = Engine.ViewHeight;

            // Vector2 mousePosition = MInput.Mouse.Position;
            // For whatever reason, MInput.Mouse.Position keeps returning 0, 0
            // Let's just use the XNA / FNA MouseState instead.
            MouseState mouseState = Mouse.GetState();
            int mouseScrollDelta = mouseState.ScrollWheelValue - mouseScroll;
            mouseScroll = mouseState.ScrollWheelValue;
            Vector2 mousePosition = new Vector2(mouseState.X, mouseState.Y);
            Vector2? mouseSnapPosition = null;

            int viewScale = 1;

            string mouseText = "";

            Level level = Engine.Scene as Level;

            if (level != null) {
                mouseText += $"Area: {level.Session.Level} @ {level.Session.Area}\n";
            }

            mouseText += $"Cursor @\n screen: {(int) Math.Round(mousePosition.X)}, {(int) Math.Round(mousePosition.Y)}";

            if (level != null) {
                Camera cam = level.Camera;
                viewScale = (int) Math.Round(Engine.Instance.GraphicsDevice.PresentationParameters.BackBufferWidth / (float) cam.Viewport.Width);
                Vector2 mouseWorldPosition = mousePosition;
                // Convert screen to world position.
                mouseWorldPosition = Calc.Floor(mouseWorldPosition / viewScale);
                mouseWorldPosition = cam.ScreenToCamera(mouseWorldPosition);
                // CelesteTAS already displays world coordinates. If it is installed, leave that up to it.
                if (!celesteTASInstalled.Value) {
                    mouseText += $"\n world:       {(int) Math.Round(mouseWorldPosition.X)}, {(int) Math.Round(mouseWorldPosition.Y)}";
                }
                mouseWorldPosition -= level.LevelOffset;
                mouseText += $"\n level:       {(int) Math.Round(mouseWorldPosition.X)}, {(int) Math.Round(mouseWorldPosition.Y)}";
                // Convert world to world-snap position.
                mouseSnapPosition = mouseWorldPosition;
                mouseSnapPosition = Calc.Floor(mouseSnapPosition.Value / 8f);
                mouseText += $"\n level, /8:   {(int) Math.Round(mouseSnapPosition.Value.X)}, {(int) Math.Round(mouseSnapPosition.Value.Y)}";
                mouseSnapPosition = 8f * mouseSnapPosition;
                mouseText += $"\n level, snap: {(int) Math.Round(mouseSnapPosition.Value.X)}, {(int) Math.Round(mouseSnapPosition.Value.Y)}";
                // Convert world-snap to screen-snap position.
                mouseSnapPosition += new Vector2(4f, 4f); // Center the cursor on the tile.
                mouseSnapPosition += level.LevelOffset;
                mouseSnapPosition = cam.CameraToScreen(mouseSnapPosition.Value);
                mouseSnapPosition *= viewScale;
            }

            Draw.SpriteBatch.Begin();

            // Draw cursor below all other UI.
            if (mouseScrollDelta < 0)
                cursorScale--;
            else if (mouseScrollDelta > 0)
                cursorScale++;
            cursorScale = Calc.Clamp(cursorScale, 1, viewScale);
            if (mouseSnapPosition != null)
                DrawCursor(mouseSnapPosition.Value, cursorScale, Color.Red);
            DrawCursor(mousePosition, cursorScale, Color.Yellow);

            // Draw cursor world position.
            Vector2 mouseTextSize = Draw.DefaultFont.MeasureString(mouseText);
            Draw.Rect(10f, 10f, mouseTextSize.X + 20f, mouseTextSize.Y + 20f, Color.Black * 0.8f);
            Draw.SpriteBatch.DrawString(
                Draw.DefaultFont,
                mouseText,
                new Vector2(20f, 20f),
                Color.White
            );

            // Draw standard console.

            Draw.Rect(10f, viewHeight - 50f, viewWidth - 20f, 40f, Color.Black * 0.8f);

            var drawPoint = new Vector2(20f, viewHeight - 42f);
            Draw.SpriteBatch.DrawString(Draw.DefaultFont, ">" + currentText, drawPoint, Color.White);
            if (underscore) {
                var size = Draw.DefaultFont.MeasureString(">" + currentText.Substring(0, charIndex));
                size.X++;
                Draw.Line(drawPoint + new Vector2(size.X, 0), drawPoint + size, Color.White);
            }

            if (drawCommands.Count > 0) {
                float height = 10f + 30f * drawCommands.Count;
                Draw.Rect(10f, viewHeight - height - 60f, viewWidth - 20f, height, Color.Black * 0.8f);
                for (int i = 0; i < drawCommands.Count; i++) {
                    Draw.SpriteBatch.DrawString(
                        Draw.DefaultFont,
                        drawCommands[i].Text,
                        new Vector2(20f, viewHeight - 92f - 30f * i),
                        drawCommands[i].Color
                    );
                }
            }

            Draw.SpriteBatch.End();
        }

        private string[] sidTabResults = new string[0];
        private int sidTabIndex = -1;

        private extern void orig_HandleKey(Keys key);
        private void HandleKey(Keys key) {
            underscore = true;
            underscoreCounter = 0f;
            bool shift = currentState[Keys.LeftShift] == KeyState.Down || currentState[Keys.RightShift] == KeyState.Down;
            bool ctrl = currentState[Keys.LeftControl] == KeyState.Down || currentState[Keys.LeftControl] == KeyState.Down;
            
            if (key == Keys.Tab && (currentText.StartsWith("load ") || currentText.StartsWith("hard ") || currentText.StartsWith("rmx2 "))) {
                // handle tab autocomplete for SIDs

                if (sidTabIndex == -1) {
                    // search for SIDs that match what we started typing.
                    string startOfSid = currentText.Substring(5);
                    sidTabResults = AreaData.Areas.Select(area => area.GetSID()).Where(sid => sid.StartsWith(startOfSid, StringComparison.InvariantCultureIgnoreCase)).ToArray();
                }

                if (sidTabResults.Length != 0) {
                    if (shift) {
                        // Shift+Tab => backwards
                        sidTabIndex--;
                    } else {
                        // Tab => forwards
                        sidTabIndex++;
                    }

                    // if sidTabIndex was -1 and we pressed Shift+Tab, we should display the last result.
                    // (if we pressed Tab instead, sidTabIndex will be 0, which is what we want.)
                    if (sidTabIndex == -2) {
                        sidTabIndex = sidTabResults.Length - 1;
                    }

                    // wrap around if gone out of bounds
                    if (sidTabIndex < 0) {
                        sidTabIndex += sidTabResults.Length;
                    }
                    sidTabIndex %= sidTabResults.Length;

                    // autocomplete
                    currentText = currentText.Substring(0, 5) + sidTabResults[sidTabIndex];
                    charIndex = currentText.Length;
                }
            } else if (key == Keys.Left || key == Keys.Right) {
                // handle seek forward and backward
                int dir = key == Keys.Left ? -1 : 1;
                charIndex = Calc.Clamp(charIndex + dir, 0, currentText.Length);
                while (ctrl && !IsWordBoundary(charIndex, dir == 1)) {
                    charIndex += dir;
                }

                MarkKeyRepeatable(key);
            } else if (key == Keys.Back) {
                // handle backspace specially so we can do ctrl-back
                bool breakSoon;
                do {
                    if (charIndex == 0) {
                        break;
                    }
                    breakSoon = IsWordBoundary(charIndex - 1, false);
                    currentText = currentText.Substring(0, Math.Max(charIndex - 1, 0)) + currentText.Substring(charIndex);
                    charIndex--;
                } while (ctrl && !breakSoon);

                MarkKeyRepeatable(key);
            } else if (key == Keys.Delete) {
                // new behavior for delete
                bool breakSoon;
                do {
                    if (charIndex == currentText.Length) {
                        break;
                    }
                    breakSoon = IsWordBoundary(charIndex + 1, true);
                    currentText = currentText.Substring(0, charIndex) + currentText.Substring(charIndex + 1);
                } while (ctrl && !breakSoon);
                
                MarkKeyRepeatable(key);
            } else if (key == Keys.Home) {
                charIndex = 0;
            } else if (key == Keys.End) {
                charIndex = currentText.Length;
            } else {
                if (key != Keys.Tab && key != Keys.LeftShift && key != Keys.RightShift && key != Keys.RightAlt && key != Keys.LeftAlt && key != Keys.RightControl && key != Keys.LeftControl) {
                    // reset the tab index: next time we press Tab, autocomplete will search matching SIDs again.
                    sidTabIndex = -1;
                }
                if (key == Keys.Enter) {
                    // If we're entering the line, process all of it please
                    charIndex = currentText.Length;
                }

                // proceed to vanilla code, operating only on the charIndex prefix of the input
                string suffix = currentText.Substring(charIndex);
                currentText = currentText.Substring(0, charIndex);
                orig_HandleKey(key);
                charIndex = currentText.Length;
                currentText += suffix;
            }
        }

        private bool IsWord(char ch) {
            // also count _ and - as wordchars. _ is standard and - appears in room names commonly
            // do not count / since a common usecase could be to edit a segment of a SID
            return char.IsLetterOrDigit(ch) || ch == '_' || ch == '-';
        }

        private bool IsWordBoundary(int idx, bool forward) {
                // for move forward that means t[i-1] is word and t[i] is nonword
                // for move backward that means t[i] is word and t[i-1] is nonword
                if (idx <= 0 || idx >= currentText.Length) {
                    return true;
                }
                char chBack = currentText[idx - 1];
                char chForward = currentText[idx];
                bool backWord = IsWord(chBack);
                bool foreWord = IsWord(chForward);
                // oof
                return (forward && backWord && !foreWord) || (!forward && !backWord && foreWord);
        }

        private void MarkKeyRepeatable(Keys key) {
            if (repeatKey == null || repeatKey != key) {
                repeatKey = key;
                repeatCounter = 0.0f;
            }
        }

        // Fix for https://github.com/EverestAPI/Everest/issues/167
        private extern void orig_EnterCommand();
        private void EnterCommand() {
            // only accept commands that will be correctly parsed
            if (!string.IsNullOrWhiteSpace(currentText.Replace(",", ""))) {
                orig_EnterCommand();
            }
        }

        [MonoModIgnore]
        private extern void LogStackTrace(string stackTrace);

        // If exception message contains characters can't be displayed tell user check log.txt for full exception message.
        [MonoModReplace]
        private void InvokeMethod(MethodInfo method, object[] param = null) {
            try {
                method.Invoke(null, param);
            } catch (Exception ex) {
                Exception innerException = ex.InnerException;

                Engine.Commands.Log(innerException.Message, Color.Yellow);
                LogStackTrace(innerException.StackTrace);

                if (ContainCantDrawChar(innerException.Message + innerException.StackTrace)) {
                    Engine.Commands.Log("Please check log.txt for full exception log, because it contains characters that can't be displayed.", Color.Yellow);
                    Logger.Log("Commands", innerException.ToString());
                }
            }
        }

        private bool ContainCantDrawChar(string text) {
            return text.ToCharArray().Any(c => !Draw.DefaultFont.Characters.Contains(c) && !char.IsControl(c));
        }

        // Only required to be defined so that we can access it.
        [MonoModIgnore]
        private struct patch_Line {
            public string Text;
            public Color Color;
            public patch_Line(string text) {
                Text = text;
                Color = Color.White;
            }
            public patch_Line(string text, Color color) {
                Text = text;
                Color = color;
            }
        }

    }
}
