#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste;
using Celeste.Mod;
using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Monocle {
    class patch_Commands : Commands {

        // We're effectively in Commands, but still need to "expose" private fields to our mod.
        private bool canOpen;
        private KeyboardState currentState;
        private bool underscore;

#pragma warning disable CS0414 // assigned but never used
        private float repeatCounter;
        private float underscoreCounter;
#pragma warning restore CS0414 // assigned but never used

        private Keys? repeatKey;
        private string currentText = "";
        private List<patch_Line> drawCommands;
        private List<string> sorted;
        private List<string> commandHistory;
        private int seekIndex;
        private Dictionary<string, patch_CommandInfo> commands;

        private int mouseScroll;
        private int cursorScale;
        private int charIndex;
        private bool installedListener;

        private int firstLineIndexToDraw;

        private static readonly Lazy<bool> celesteTASInstalled = new Lazy<bool>(() => Everest.Modules.Any(module => module.Metadata?.Name == "CelesteTAS"));

        private extern void orig_ProcessMethod(MethodInfo method);
        private void ProcessMethod(MethodInfo method) {
            try {
                orig_ProcessMethod(method);
            } catch (Exception e) {
                // we probably met a method with some missing optional dependency, so just skip it.
                Logger.Log(LogLevel.Warn, "commands", "Could not look for custom commands in method " + method.Name);
                Logger.LogDetailed(e);
            }
        }

        [MonoModReplace] // Don't create orig_ method.
        internal void UpdateClosed() {
            if (!canOpen) {
                canOpen = true;
            // Original code only checks OemTillde and Oem8, leaving QWERTZ users in the dark...
            } else if (MInput.Keyboard.Pressed(Keys.OemTilde, Keys.Oem8) || CoreModule.Settings.DebugConsole.Pressed) {
                Open = true;
                currentState = Keyboard.GetState();
                if (!installedListener) {
                    // this should realistically be done in the constructor. if we ever patch the ctor move it there!
                    installedListener = true;
                    TextInput.OnInput += HandleChar;
                }
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
            // When the console is opening, MInput.Mouse.UpdateNull is called so MInput.Mouse.Position keeps returning 0, 0
            // Let's just use the XNA / FNA MouseState instead.
            MouseState mouseState = Mouse.GetState();
            int mouseScrollDelta = mouseState.ScrollWheelValue - mouseScroll;
            mouseScroll = mouseState.ScrollWheelValue;
            Vector2 mousePosition = new Vector2(mouseState.X, mouseState.Y);
            Vector2? mouseSnapPosition = null;

            int maxCursorScale = 1;

            string mouseText = "";

            Level level = Engine.Scene as Level;

            if (level != null) {
                mouseText += $"Area: {level.Session.Level} @ {level.Session.Area}\n";
            }

            mouseText += $"Cursor @\n screen: {(int) Math.Round(mousePosition.X)}, {(int) Math.Round(mousePosition.Y)}";

            if (level != null) {
                Camera cam = level.Camera;
                float viewScale = (float) Engine.ViewWidth / Engine.Width;
                // Convert screen to world position. The method assumes screen is in full size (1920x1080) so we need to scale the position.
                Vector2 mouseWorldPosition = Calc.Floor(((patch_Level) level).ScreenToWorld(mousePosition / viewScale));
                // CelesteTAS already displays world coordinates. If it is installed, leave that up to it.
                if (!celesteTASInstalled.Value) {
                    mouseText += $"\n world:       {(int) Math.Round(mouseWorldPosition.X)}, {(int) Math.Round(mouseWorldPosition.Y)}";
                }
                mouseWorldPosition -= level.LevelOffset;
                mouseText += $"\n level:       {(int) Math.Round(mouseWorldPosition.X)}, {(int) Math.Round(mouseWorldPosition.Y)}";
                // Convert world to world-snap position.
                mouseSnapPosition = Calc.Floor(mouseWorldPosition / 8f);
                mouseText += $"\n level, /8:   {(int) Math.Round(mouseSnapPosition.Value.X)}, {(int) Math.Round(mouseSnapPosition.Value.Y)}";
                mouseSnapPosition = 8f * mouseSnapPosition;
                mouseText += $"\n level, snap: {(int) Math.Round(mouseSnapPosition.Value.X)}, {(int) Math.Round(mouseSnapPosition.Value.Y)}";
                // Convert world-snap to screen-snap position.
                mouseSnapPosition += new Vector2(4f, 4f); // Center the cursor on the tile.
                mouseSnapPosition += level.LevelOffset;
                mouseSnapPosition = Calc.Floor(((patch_Level) level).WorldToScreen(mouseSnapPosition.Value) * viewScale);
                // Cursor shouldn't be larger than an unzoomed tile (level.Zoom and cam.Zoom are both 1)
                maxCursorScale = Engine.ViewWidth / cam.Viewport.Width;
            }

            Draw.SpriteBatch.Begin();

            // Draw cursor below all other UI.
            if (mouseScrollDelta < 0)
                cursorScale--;
            else if (mouseScrollDelta > 0)
                cursorScale++;
            cursorScale = Calc.Clamp(cursorScale, 1, maxCursorScale);
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
                int drawCount = Math.Min((viewHeight - 100) / 30, drawCommands.Count - firstLineIndexToDraw);
                float height = 10f + 30f * drawCount;
                Draw.Rect(10f, viewHeight - height - 60f, viewWidth - 20f, height, Color.Black * 0.8f);
                for (int i = 0; i < drawCount && firstLineIndexToDraw + i < drawCommands.Count; i++) {
                    Draw.SpriteBatch.DrawString(
                        Draw.DefaultFont,
                        drawCommands[firstLineIndexToDraw + i].Text,
                        new Vector2(20f, viewHeight - 92f - 30f * i),
                        drawCommands[firstLineIndexToDraw + i].Color
                    );
                }
            }

            Draw.SpriteBatch.End();
        }

        private string[] tabResults = new string[0];
        private int tabIndex = -1;
        private string tabPrefix = "";

        [MonoModReplace]  // don't create an orig_ method
        private void HandleKey(Keys key) {
            // this method handles all control characters, which go through the XNA Keys API
            underscore = true;
            underscoreCounter = 0f;
            bool shift = currentState[Keys.LeftShift] == KeyState.Down || currentState[Keys.RightShift] == KeyState.Down;
            bool ctrl = currentState[Keys.LeftControl] == KeyState.Down || currentState[Keys.RightControl] == KeyState.Down;
            bool breakSoon;

            // handle tab aborting
            // nuance: this method is technically called for character keys, not just control keys
            switch (key) {
                case Keys.Tab:
                case Keys.LeftShift:
                case Keys.RightShift:
                case Keys.LeftControl:
                case Keys.RightControl:
                case Keys.LeftAlt:
                case Keys.RightAlt:
                    break;
                default:
                    tabIndex = -1;
                    break;
            }
            
            // all keys should be repeatable except for enter (and stuff not handled by this function)
            if (key != Keys.Enter && (repeatKey == null || repeatKey != key)) {
                repeatKey = key;
                repeatCounter = 0.0f;
            }
            
            // handle main functionality
            switch (key) {
                case Keys.Enter:
                    if (currentText.Length > 0)
                        EnterCommand();
                    charIndex = currentText.Length;
                    seekIndex = -1;
                    break;
                case Keys.Tab:
                    if (tabIndex == -1) {
                        // pressed tab for fresh session - query for tabbable set
                        if (currentText.StartsWith("load ") || currentText.StartsWith("hard ") || currentText.StartsWith("rmx2 ")) {
                            // SID matching
                            tabPrefix = currentText.Substring(0, 5);
                            string startOfSid = currentText.Substring(5);
                            tabResults = AreaData.Areas.Select(area => area.GetSID()).Where(sid => sid.StartsWith(startOfSid, StringComparison.InvariantCultureIgnoreCase)).ToArray();
                        } else {
                            // command matching
                            tabPrefix = "";
                            tabResults = sorted.Where(cmd => cmd.StartsWith(currentText, StringComparison.InvariantCultureIgnoreCase)).ToArray();
                        }

                        if (tabResults.Length == 0) {
                            // No matches. abort
                            break;
                        }

                        // set initial index
                        tabIndex = shift ? tabResults.Length - 1 : 0;
                    } else {
                        // pressed tab in existing session - scroll through the list
                        int tdir = shift ? -1 : 1;
                        tabIndex += tdir;
                        if (tabIndex < 0) {
                            tabIndex += tabResults.Length;
                        }
                        tabIndex %= tabResults.Length;
                    }
                    
                    // by this point tabIndex should be valid. perform a completion
                    currentText = tabPrefix + tabResults[tabIndex];
                    charIndex = currentText.Length;
                    break;
                case Keys.Left:
                case Keys.Right:
                    int dir = key == Keys.Left ? -1 : 1;
                    charIndex = Calc.Clamp(charIndex + dir, 0, currentText.Length);
                    while (ctrl && !IsWordBoundary(charIndex, dir == 1)) {
                        charIndex += dir;
                    }
                    break;
                case Keys.Back:
                    do {
                        if (charIndex == 0) {
                            break;
                        }
                        breakSoon = IsWordBoundary(charIndex - 1, false);
                        currentText = currentText.Substring(0, Math.Max(charIndex - 1, 0)) + currentText.Substring(charIndex);
                        charIndex--;
                    } while (ctrl && !breakSoon);
                    break;
                case Keys.Delete:
                    do {
                        if (charIndex == currentText.Length) {
                            break;
                        }
                        breakSoon = IsWordBoundary(charIndex + 1, true);
                        currentText = currentText.Substring(0, charIndex) + currentText.Substring(charIndex + 1);
                    } while (ctrl && !breakSoon);
                    break;
                case Keys.Home:
                    if (ctrl) {
                        firstLineIndexToDraw = Math.Max(drawCommands.Count - 1, 0);
                    } else {
                        charIndex = 0;
                    }
                    break;
                case Keys.End:
                    if (ctrl) {
                        firstLineIndexToDraw = 0;
                    } else {
                        charIndex = currentText.Length;
                    }
                    break;
                case Keys.Up:
                case Keys.Down:
                    int hdir = key == Keys.Up ? 1 : -1;
                    if (ctrl) {
                        firstLineIndexToDraw = Calc.Clamp(firstLineIndexToDraw + hdir, 0, Math.Max(drawCommands.Count - 1, 0));
                    } else {
                        seekIndex = Calc.Clamp(seekIndex + hdir, -1, commandHistory.Count - 1);
                        currentText = seekIndex == -1 ? "" : commandHistory[seekIndex];
                        charIndex = currentText.Length;
                    }
                    break;
                case Keys.F1:
                case Keys.F2:
                case Keys.F3:
                case Keys.F4:
                case Keys.F5:
                case Keys.F6:
                case Keys.F7:
                case Keys.F8:
                case Keys.F9:
                case Keys.F10:
                case Keys.F11:
                case Keys.F12:
                    ExecuteFunctionKeyAction(key - Keys.F1);
                    break;
            }
        }

        private void HandleChar(char key) {
            // this API seemingly handles repeating keys for us
            if (!Open) {
                return;
            }
            if (key == '~' || key == '`') {
                Open = canOpen = false;
                return;
            }
            if (char.IsControl(key)) {
                return;
            }

            currentText = currentText.Substring(0, charIndex) + key + currentText.Substring(charIndex);
            charIndex++;
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

        [MonoModIgnore]
        [PatchCommandsUpdateOpen]
        internal extern void UpdateOpen();

        [MonoModIgnore]
        private extern void BuildCommandsList();

        public void ReloadCommandsList() {
            commands.Clear();
            sorted.Clear();
            BuildCommandsList();
        }

        [MonoModReplace]
        public new void Log(object obj, Color color) {
            string text = obj.ToString();
            if (text.Contains("\n")) {
                foreach (string obj2 in text.Split('\n')) {
                    Log(obj2, color);
                }
                return;
            }
            int width = Engine.ViewWidth - 40;
            while (Draw.DefaultFont.MeasureString(text).X > width) {
                int index = -1;
                for (int i = 0; i < text.Length; i++) {
                    if (text[i] == ' ') {
                        if (Draw.DefaultFont.MeasureString(text.Substring(0, i)).X > width) {
                            break;
                        }
                        index = i;
                    }
                }
                if (index == -1) {
                    break;
                }
                drawCommands.Insert(0, new patch_Line(text.Substring(0, index), color));
                text = text.Substring(index + 1);
            }
            drawCommands.Insert(0, new patch_Line(text, color));
            int maxCommandLines = Math.Max(CoreModule.Settings.ExtraCommandHistoryLines + (Engine.ViewHeight - 100) / 30, 0);
            firstLineIndexToDraw = Calc.Clamp(firstLineIndexToDraw, 0, Math.Max(drawCommands.Count - 1, 0));
            while (drawCommands.Count > maxCommandLines) {
                drawCommands.RemoveAt(drawCommands.Count - 1);
            }
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

        [MonoModIgnore]
        private struct patch_CommandInfo {
            public Action<string[]> Action;
            public string Help;
            public string Usage;
        }

    }
}

namespace MonoMod {
    /// <summary>
    /// Patches Commands.UpdateOpen to make key's repeat timer independent with time rate.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchCommandsUpdateOpen))]
    class PatchCommandsUpdateOpenAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchCommandsUpdateOpen(ILContext il, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(il);

            TypeDefinition t_Engine = MonoModRule.Modder.FindType("Monocle.Engine").Resolve();
            MethodReference m_get_RawDeltaTime = t_Engine.FindMethod("System.Single get_RawDeltaTime()");

            while (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCall("Monocle.Engine", "get_DeltaTime"))) {
                cursor.Next.Operand = m_get_RawDeltaTime;
            }
        }

    }
}
