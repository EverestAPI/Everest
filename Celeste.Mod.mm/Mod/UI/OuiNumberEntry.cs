using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;

namespace Celeste.Mod.UI {
    /// <summary>
    /// Modification of <see cref="OuiModOptionString"/> to handle numeric input.
    /// </summary>
    public class OuiNumberEntry : Oui, OuiModOptions.ISubmenu {
        // Value is handled internally as a string
        public string StartingValue;

        private string _Value;
        public string Value {
            get {
                return _Value;
            }
            set {
                _Value = value;
                OnValueChange?.Invoke(float.Parse(value));
            }
        }
        public int MaxValueLength;

        public event Action<float> OnValueChange;
        private Action exit;

        private bool allowDecimals;
        private bool allowNegatives;

        private string[] letters;

        private float widestLetter;
        private float widestLine;
        private int widestLineCount;
        private float lineHeight;
        private float lineSpacing;
        private float boxPadding;
        private float optionsScale;
        private float optionsWidth;
        private float keyboardWidth;
        private float boxWidth;
        private float boxHeight;

        private string cancel;
        private float cancelWidth;
        private string backspace;
        private float backspaceWidth;
        private string accept;
        private float acceptWidth;

        private int index = 0;
        private int line = 0;
        private int optionsIndex;
        private bool selectingOptions = true;

        private float pressedTimer;
        private float timer;
        private float ease;
        private Wiggler wiggler;

        private Color unselectColor = Color.LightGray;
        private Color selectColorA = Calc.HexToColor("84FF54");
        private Color selectColorB = Calc.HexToColor("FCFF59");
        private Color disableColor = Color.DarkSlateBlue;

        private Vector2 boxTopLeft {
            get {
                return Position + new Vector2((1920f - boxWidth) / 2f, 360f + (680f - boxHeight) / 2f);
            }
        }
        private Vector2 keyboardTopLeft {
            get {
                return Position + new Vector2((1920f - keyboardWidth) / 2f, 360f + (680f - boxHeight) / 2f);
            }
        }

        public OuiNumberEntry()
            : base() {

            wiggler = Wiggler.Create(0.25f, 4f);
            Position = new Vector2(0f, 1080f);
            Visible = false;
        }

        /// <summary>
        /// Sets up the OuiNumberEntry screen.
        /// </summary>
        /// <typeparam name="T">Oui to return to on exit</typeparam>
        /// <param name="value">Initial value</param>
        /// <param name="onValueChange">Action to be called when a new value is set.</param>
        /// <param name="maxValueLength">The number of digits allowed, excluding "-" and "."</param>
        /// <param name="allowDecimals">If decimal numbers should be allowed</param>
        /// <param name="allowNegatives">If negative numbers should be allowed</param>
        /// <returns></returns>
        public OuiNumberEntry Init<T>(float value, Action<float> onValueChange,
            int maxValueLength = 6, bool allowDecimals = true, bool allowNegatives = true) where T : Oui {
            _Value = StartingValue = value.ToString($"F{maxValueLength}").TrimEnd('0').TrimEnd('.');
            OnValueChange = onValueChange;

            MaxValueLength = maxValueLength;

            exit = () => Overworld.Goto<T>();

            // These don't prevent a negative/decimal number from being passed in via value
            // They just disable '.' and '-'
            this.allowDecimals = allowDecimals;
            this.allowNegatives = allowNegatives;

            return this;
        }

        public override IEnumerator Enter(Oui from) {
            if (UseKeyboardInput) 
                TextInput.OnInput += OnTextInput;

            Overworld.ShowInputUI = false;

            selectingOptions = false;
            optionsIndex = 0;
            index = 0;
            line = 0;

            // Create the keyboard, and take the measurements for it.
            string letterChars = "7 8 9\n4 5 6\n1 2 3\n- 0 .";
            letters = letterChars.Split('\n');

            foreach (char c in letterChars) {
                float width = ActiveFont.Measure(c).X;
                if (width > widestLetter) {
                    widestLetter = width;
                }
            }

            widestLineCount = 0;
            foreach (string letter in letters) {
                if (letter.Length > widestLineCount) {
                    widestLineCount = letter.Length;
                }
            }

            widestLine = widestLineCount * widestLetter;
            letterChars = null;
            boxPadding = widestLetter;
            keyboardWidth = widestLine + boxPadding * 2f;

            lineHeight = ActiveFont.LineHeight;
            lineSpacing = ActiveFont.LineHeight * 0.1f;

            // take the measurements for options.
            optionsScale = 0.75f;
            cancel = Dialog.Clean("name_back");
            backspace = Dialog.Clean("name_backspace");
            accept = Dialog.Clean("name_accept");
            cancelWidth = ActiveFont.Measure(cancel).X * optionsScale;
            backspaceWidth = ActiveFont.Measure(backspace).X * optionsScale;
            acceptWidth = ActiveFont.Measure(accept).X * optionsScale;
            optionsWidth = cancelWidth + backspaceWidth + acceptWidth + widestLetter * 3f;

            boxWidth = Math.Max(widestLine, optionsWidth) + boxPadding * 2f;
            boxHeight = (letters.Length + 1f) * lineHeight + letters.Length * lineSpacing + boxPadding * 3f;

            Visible = true;

            // Ease the keyboard in.
            Vector2 posFrom = Position;
            Vector2 posTo = Vector2.Zero;
            for (float t = 0f; t < 1f; t += Engine.DeltaTime * 2f) {
                ease = Ease.CubeIn(t);
                Position = posFrom + (posTo - posFrom) * Ease.CubeInOut(t);
                yield return null;
            }
            ease = 1f;
            posFrom = Vector2.Zero;
            posTo = Vector2.Zero;

            yield return 0.2f;

            Focused = true;

            yield return 0.2f;

            wiggler.Start();
        }

        public override IEnumerator Leave(Oui next) {
            // Non existent unhooks aren't dangerous, and can get us out of weird situations
            TextInput.OnInput -= OnTextInput;

            Overworld.ShowInputUI = true;
            Focused = false;

            // Ease out.
            Vector2 posFrom = Position;
            Vector2 posTo = new Vector2(0f, 1080f);
            for (float t = 0f; t < 1f; t += Engine.DeltaTime * 2f) {
                ease = 1f - Ease.CubeIn(t);
                Position = posFrom + (posTo - posFrom) * Ease.CubeInOut(t);
                yield return null;
            }

            Visible = false;
        }

        public bool UseKeyboardInput {
            get {
                var settings = Core.CoreModule.Instance._Settings as Core.CoreModuleSettings;
                return settings?.UseKeyboardForTextInput ?? false;
            }
        }

        public void OnTextInput(char c) {
            if (c == (char) 13) {
                // Enter - confirm.
                Finish();

            } else if (c == (char) 8) {
                // Backspace - trim
                Backspace();
            } else if (c == (char) 127) {
                // Delete - currenly not handled.

            } else if (c == '.') {
                // Add decimal, only one '.' allowed in the value string
                if (allowDecimals && Value.Length < TrueMaxLength() && !Value.Contains(".")) {
                    Audio.Play(SFX.ui_main_rename_entry_space);
                    Value += c;
                } else {
                    Audio.Play(SFX.ui_main_button_invalid);
                }

            } else if (c == '-') {
                // Toggle negative number
                if (allowNegatives) {
                    if (!Value.StartsWith("-"))
                        Value = string.Concat("-", Value);
                    else
                        Value = Value.Substring(1);
                    Audio.Play(SFX.ui_main_rename_entry_space);
                } else
                    Audio.Play(SFX.ui_main_button_invalid);
            } else if (char.IsDigit(c)) {
                // Any other digit - append.
                if (Value.Length < TrueMaxLength() && ActiveFont.FontSize.Characters.ContainsKey(c)) {
                    Audio.Play(SFX.ui_main_rename_entry_char);
                    if (Value.Equals("0"))
                        Value = c.ToString();
                    else if (Value.Equals("-0"))
                        Value = "-" + c;
                    else
                        Value += c;
                } else {
                    Audio.Play(SFX.ui_main_button_invalid);
                }
            } else if (!char.IsControl(c)) {
                Audio.Play(SFX.ui_main_button_invalid);
            }
        }

        private int TrueMaxLength() {
            int trueMaxLength = MaxValueLength;
            if (Value.Contains("-")) {
                trueMaxLength++;
            }
            if (Value.Contains(".")) {
                trueMaxLength++;
            }
            return trueMaxLength;
        }

        public override void Update() {
            bool wasFocused = Focused;
            // Only "focus" if we're not using the keyboard for input
            Focused = wasFocused && !UseKeyboardInput;

            base.Update();

            // TODO: Rewrite or study and document the following code.
            // It stems from OuiFileNaming.

            if (!(Selected && Focused)) {
                goto End;
            }

            if (Input.MenuRight.Pressed && (optionsIndex < 2 || !selectingOptions)) {
                if (selectingOptions) {
                    // move 1 option right.
                    optionsIndex = Math.Min(optionsIndex + 1, 2);
                } else {
                    // move right on the keyboard until hitting content.
                    do {
                        index = (index + 1) % letters[line].Length;
                    } while (!isOptionValid(line, index));
                }
                wiggler.Start();
                Audio.Play(SFX.ui_main_rename_entry_roll);

            } else if (Input.MenuLeft.Pressed && (optionsIndex > 0 || !selectingOptions)) {
                if (selectingOptions) {
                    // move 1 option left.
                    optionsIndex = Math.Max(optionsIndex - 1, 0);
                } else {
                    // move left on the keyboard until hitting content.
                    do {
                        index = (index + letters[line].Length - 1) % letters[line].Length;
                    } while (!isOptionValid(line, index));
                }
                wiggler.Start();
                Audio.Play(SFX.ui_main_rename_entry_roll);

            } else if (Input.MenuDown.Pressed && !selectingOptions) {
                int lineNext = line + 1;
                bool shouldSwitchToOptions = true;

                // scroll down until hitting a valid character
                for (; lineNext < letters.Length; lineNext++) {
                    if (index < letters[lineNext].Length && isOptionValid(lineNext, index)) {
                        shouldSwitchToOptions = false;
                        break;
                    }
                }

                if (shouldSwitchToOptions) {
                    // we scrolled down to the bottom; switch to options
                    selectingOptions = true;
                    // select the middle option; it's always the closest one because the keyboard is narrow enough.
                    optionsIndex = 1;
                } else {
                    line = lineNext;
                }

                wiggler.Start();
                Audio.Play(SFX.ui_main_rename_entry_roll);

            } else if ((Input.MenuUp.Pressed || (selectingOptions && Value.Length <= 0 && optionsIndex > 0)) && (line > 0 || selectingOptions)) {
                if (selectingOptions) {
                    // we were in options and pressed Up; go back to the bottom of the keyboard.
                    line = letters.Length;
                    selectingOptions = false;

                    // if we selected option 0, focus on the leftmost row; if we selected option 1, focus on the middle row; if we selected option 2, focus on the rightmost row 
                    index = optionsIndex * 2;
                }

                // go up until hitting a valid character
                do {
                    line--;
                } while (line > 0 && (index >= letters[line].Length || !isOptionValid(line, index)));

                // go left until hitting a valid character
                while (index >= letters[line].Length || !isOptionValid(line, index)) {
                    index--;
                }

                wiggler.Start();
                Audio.Play(SFX.ui_main_rename_entry_roll);

            } else if (Input.MenuConfirm.Pressed) {
                if (selectingOptions) {
                    if (optionsIndex == 0) {
                        Cancel();
                    } else if (optionsIndex == 1) {
                        Backspace();
                    } else if (optionsIndex == 2) {
                        Finish();
                    }
                } else {
                    OnTextInput(letters[line][index]);
                }

            } else if (Input.MenuCancel.Pressed) {
                if (Value.Length > 0) {
                    Backspace();
                } else {
                    Cancel();
                }

            } else if (Input.Pause.Pressed) {
                Finish();
            }

            End:

            if (wasFocused && !Focused) {
                if (Input.ESC) {
                    Cancel();
                    wasFocused = false;
                }
            }

            Focused = wasFocused;

            pressedTimer -= Engine.DeltaTime;
            timer += Engine.DeltaTime;
            wiggler.Update();
        }

        private bool isOptionValid(int line, int index) {
            return letters[line][index] != ' '
                && (letters[line][index] != '-' || allowNegatives)
                && (letters[line][index] != '.' || allowDecimals);
        }

        private void Backspace() {
            if (Value.Length > 1) {
                // temp variable used to avoid trying to parse "-" as float
                string temp = Value.Substring(0, Value.Length - 1);
                if (temp.Equals("-")) {
                    Value = "0";
                } else {
                    Value = temp;
                }
                Audio.Play(SFX.ui_main_rename_entry_backspace);
            } else if (Value.Length == 1 && !Value.Equals("0")) {
                Value = "0";
                Audio.Play(SFX.ui_main_rename_entry_backspace);
            } else {
                Audio.Play(SFX.ui_main_button_invalid);
            }
        }

        private void Finish() {
            Focused = false;
            exit?.Invoke();
            Audio.Play(SFX.ui_main_rename_entry_accept);
        }

        private void Cancel() {
            Value = StartingValue;
            Focused = false;
            exit?.Invoke();
            Audio.Play(SFX.ui_main_button_back);
        }

        public override void Render() {
            int prevIndex = index;
            // Only "focus" if we're not using the keyboard for input
            if (UseKeyboardInput)
                index = -1;

            Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.8f * ease);

            // draw the keyboard
            Vector2 drawingPosition = keyboardTopLeft + new Vector2(boxPadding, boxPadding);
            int letterIndex = 0;
            foreach (string letter in letters) {
                for (int i = 0; i < letter.Length; i++) {
                    bool selected = letterIndex == line && i == index && !selectingOptions;
                    Vector2 scale = Vector2.One * (selected ? 1.7f : 1.4f);
                    Vector2 posLetter = drawingPosition + new Vector2(widestLetter, lineHeight) / 2f;
                    if (selected) {
                        posLetter += new Vector2(0f, wiggler.Value) * 8f;
                    }
                    DrawOptionText(letter[i].ToString(), posLetter, new Vector2(0.5f, 0.5f), scale, selected);
                    drawingPosition.X += widestLetter;
                }
                drawingPosition.X = keyboardTopLeft.X + boxPadding;
                drawingPosition.Y += lineHeight + lineSpacing * 1.4f;
                letterIndex++;
            }

            float wiggle = wiggler.Value * 8f;

            // draw the boundary line between keyboard and options
            drawingPosition.X = boxTopLeft.X + boxPadding;
            drawingPosition.Y = boxTopLeft.Y + boxHeight - lineHeight - boxPadding;
            Draw.Rect(drawingPosition.X, drawingPosition.Y - boxPadding * 0.5f, boxWidth - boxPadding * 2f, 4f, Color.White);

            // draw the 3 options
            DrawOptionText(cancel, drawingPosition + new Vector2(15f, lineHeight + ((selectingOptions && optionsIndex == 0) ? wiggle : 0f)), new Vector2(0f, 1f), Vector2.One * optionsScale, selectingOptions && optionsIndex == 0, !Focused);
            drawingPosition.X = boxTopLeft.X + boxWidth - backspaceWidth - widestLetter - widestLetter - acceptWidth - boxPadding;

            DrawOptionText(backspace, drawingPosition + new Vector2(15f, lineHeight + ((selectingOptions && optionsIndex == 1) ? wiggle : 0f)), new Vector2(0f, 1f), Vector2.One * optionsScale, selectingOptions && optionsIndex == 1, Value.Length <= 0 || !Focused);
            drawingPosition.X += backspaceWidth + widestLetter;

            DrawOptionText(accept, drawingPosition + new Vector2(10f, lineHeight + ((selectingOptions && optionsIndex == 2) ? wiggle : 0f)), new Vector2(0f, 1f), Vector2.One * optionsScale, selectingOptions && optionsIndex == 2, Value.Length < 1 || !Focused);

            // draw the current value
            ActiveFont.DrawEdgeOutline(Value, Position + new Vector2(960f, 286f), new Vector2(0.5f, 0.5f), Vector2.One * 2f, Color.Gray, 4f, Color.DarkSlateBlue, 2f, Color.Black);

            index = prevIndex;
        }

        private void DrawOptionText(string text, Vector2 at, Vector2 justify, Vector2 scale, bool selected, bool disabled = false) {
            // Only draw "interactively" if not using the keyboard for input
            // Also grey out invalid keys ("-" if negatives are forbidden, "." if decimals are forbidden).
            if (UseKeyboardInput || (text.Equals("-") && !allowNegatives) || (text.Equals(".") && !allowDecimals)) {
                selected = false;
                disabled = true;
            }

            Color color = disabled ? disableColor : GetTextColor(selected);
            Color edgeColor = disabled ? Color.Lerp(disableColor, Color.Black, 0.7f) : Color.Gray;
            if (selected && pressedTimer > 0f) {
                ActiveFont.Draw(text, at + Vector2.UnitY, justify, scale, color);
            } else {
                ActiveFont.DrawEdgeOutline(text, at, justify, scale, color, 4f, edgeColor);
            }
        }

        private Color GetTextColor(bool selected) {
            if (selected)
                return (Calc.BetweenInterval(timer, 0.1f) ? selectColorA : selectColorB);
            return unselectColor;
        }

    }
}
