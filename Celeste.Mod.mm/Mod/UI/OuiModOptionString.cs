using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;

namespace Celeste.Mod.UI {
    /// <summary>
    /// Wrapper of <see cref="OuiModOptionString"/> for consistent naming
    /// </summary>
    public class OuiTextEntry : OuiModOptionString { }

    // Based on OuiFileNaming
    public class OuiModOptionString : Oui, OuiModOptions.ISubmenu {

        // TODO: OuiModOptionString is a hellscape of decompiled code.

        public static bool Cancelled;

        public string StartingValue;

        private string _Value;
        public string Value {
            get {
                return _Value;
            }
            set {
                _Value = value;
                OnValueChange?.Invoke(value);
            }
        }

        public int MaxValueLength;
        public int MinValueLength;

        public event Action<string> OnValueChange;

        public event Action<bool> OnExit;

        private string[] letters;
        private int index = 0;
        private int line = 0;
        private float widestLetter;
        private float widestLine;
        private int widestLineCount;
        private bool selectingOptions = true;
        private int optionsIndex;
        private float lineHeight;
        private float lineSpacing;
        private float boxPadding;
        private float optionsScale;
        private string cancel;
        private string space;
        private string backspace;
        private string accept;
        private float cancelWidth;
        private float spaceWidth;
        private float backspaceWidth;
        private float beginWidth;
        private float optionsWidth;
        private float boxWidth;
        private float boxHeight;
        private float pressedTimer;
        private float timer;
        private float ease;

        private Wiggler wiggler;

        private Color unselectColor = Color.LightGray;
        private Color selectColorA = Calc.HexToColor("84FF54");
        private Color selectColorB = Calc.HexToColor("FCFF59");
        private Color disableColor = Color.DarkSlateBlue;

        private Vector2 boxtopleft {
            get {
                return Position + new Vector2((1920f - boxWidth) / 2f, 360f + (680f - boxHeight) / 2f);
            }
        }

        public OuiModOptionString()
            : base() {
            wiggler = Wiggler.Create(0.25f, 4f);
            Position = new Vector2(0f, 1080f);
            Visible = false;
        }
        
        public OuiModOptionString Init<T>(string value, Action<string> onValueChange) where T : Oui {
            return Init<T>(value, onValueChange, 12, 1);
        }

        public OuiModOptionString Init<T>(string value, Action<string> onValueChange, int maxValueLength) where T : Oui {
            return Init<T>(value, onValueChange, maxValueLength, 1);
        }

        public OuiModOptionString Init<T>(string value, Action<string> onValueChange, int maxValueLength, int minValueLength) where T : Oui {
            return Init(value, onValueChange, (confirm) => Overworld.Goto<T>(), maxValueLength, minValueLength);
        }

        public OuiModOptionString Init<T>(string value, Action<string> onValueChange, Action<bool> onExit, int maxValueLength, int minValueLength) where T : Oui {
            return Init(value, onValueChange, (confirm) => { Overworld.Goto<T>(); onExit?.Invoke(confirm); }, maxValueLength, minValueLength);
        }

        public OuiModOptionString Init(string value, Action<string> onValueChange, Action<bool> exit, int maxValueLength, int minValueLength) {
            _Value = StartingValue = value ?? "";
            OnValueChange = onValueChange;

            MaxValueLength = maxValueLength;
            MinValueLength = minValueLength;

            OnExit += exit;
            Cancelled = false;

            return this;
        }

        public override IEnumerator Enter(Oui from) {
            TextInput.OnInput += OnTextInput;

            Overworld.ShowInputUI = false;

            Engine.Commands.Enabled = false;

            selectingOptions = false;
            optionsIndex = 0;
            index = 0;
            line = 0;

            string letterChars = Dialog.Clean("name_letters");
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

            lineHeight = ActiveFont.LineHeight;
            lineSpacing = ActiveFont.LineHeight * 0.1f;
            boxPadding = widestLetter;
            optionsScale = 0.75f;
            cancel = Dialog.Clean("name_back");
            space = Dialog.Clean("name_space");
            backspace = Dialog.Clean("name_backspace");
            accept = Dialog.Clean("name_accept");
            cancelWidth = ActiveFont.Measure(cancel).X * optionsScale;
            spaceWidth = ActiveFont.Measure(space).X * optionsScale;
            backspaceWidth = ActiveFont.Measure(backspace).X * optionsScale;
            beginWidth = ActiveFont.Measure(accept).X * optionsScale;
            optionsWidth = cancelWidth + spaceWidth + backspaceWidth + beginWidth + widestLetter * 3f;
            boxWidth = Math.Max(widestLine, optionsWidth) + boxPadding * 2f;
            boxHeight = (letters.Length + 1f) * lineHeight + letters.Length * lineSpacing + boxPadding * 3f;

            Visible = true;

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
            TextInput.OnInput -= OnTextInput;

            Overworld.ShowInputUI = true;
            Focused = false;

            Engine.Commands.Enabled = (Celeste.PlayMode == Celeste.PlayModes.Debug);

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
            if (!UseKeyboardInput) {
                return;
            }

            if (c == (char) 13) {
                // Enter - confirm.
                Finish();

            } else if (c == (char) 8) {
                // Backspace - trim.
                Backspace();

            } else if (c == (char) 22) {
                // Paste.
                string value = Value + TextInput.GetClipboardText();
                if (value.Length > MaxValueLength)
                    value = value.Substring(0, MaxValueLength);
                Value = value;

            } else if (c == (char) 127) {
                // Delete - currenly not handled.

            } else if (c == ' ') {
                // Space - append.
                if (Value.Length < MaxValueLength) {
                    Audio.Play(SFX.ui_main_rename_entry_space);
                    Value += c;
                } else {
                    Audio.Play(SFX.ui_main_button_invalid);
                }

            } else if (!char.IsControl(c)) {
                // Any other character - append.
                if (Value.Length < MaxValueLength && ActiveFont.FontSize.Characters.ContainsKey(c)) {
                    Audio.Play(SFX.ui_main_rename_entry_char);
                    Value += c;
                } else {
                    Audio.Play(SFX.ui_main_button_invalid);
                }
            }
        }

        public override void SceneEnd(Scene scene) {
            Overworld.ShowInputUI = true;
            Engine.Commands.Enabled = (Celeste.PlayMode == Celeste.PlayModes.Debug);
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

            if (Input.MenuRight.Pressed && (optionsIndex < 3 || !selectingOptions) && (Value.Length > 0 || !selectingOptions)) {
                if (selectingOptions) {
                    optionsIndex = Math.Min(optionsIndex + 1, 3);
                } else {
                    do {
                        index = (index + 1) % letters[line].Length;
                    } while (letters[line][index] == ' ');
                }
                wiggler.Start();
                Audio.Play(SFX.ui_main_rename_entry_roll);

            } else if (Input.MenuLeft.Pressed && (optionsIndex > 0 || !selectingOptions)) {
                if (selectingOptions) {
                    optionsIndex = Math.Max(optionsIndex - 1, 0);
                } else {
                    do {
                        index = (index + letters[line].Length - 1) % letters[line].Length;
                    } while (letters[line][index] == ' ');
                }
                wiggler.Start();
                Audio.Play(SFX.ui_main_rename_entry_roll);

            } else if (Input.MenuDown.Pressed && !selectingOptions) {
                int lineNext = line + 1;
                bool something = true;
                for (; lineNext < letters.Length; lineNext++) {
                    if (index < letters[lineNext].Length && letters[lineNext][index] != ' ') {
                        something = false;
                        break;
                    }
                }

                if (something) {
                    selectingOptions = true;

                } else {
                    line = lineNext;

                }

                if (selectingOptions) {
                    float pos = index * widestLetter;
                    float offs = boxWidth - boxPadding * 2f;
                    if (Value.Length == 0 || pos < cancelWidth + (offs - cancelWidth - beginWidth - backspaceWidth - spaceWidth - widestLetter * 3f) / 2f) {
                        optionsIndex = 0;
                    } else if (pos < offs - beginWidth - backspaceWidth - widestLetter * 2f) {
                        optionsIndex = 1;
                    } else if (pos < offs - beginWidth - widestLetter) {
                        optionsIndex = 2;
                    } else {
                        optionsIndex = 3;
                    }
                }

                wiggler.Start();
                Audio.Play(SFX.ui_main_rename_entry_roll);

            } else if ((Input.MenuUp.Pressed || (selectingOptions && Value.Length <= 0 && optionsIndex > 0)) && (line > 0 || selectingOptions)) {
                if (selectingOptions) {
                    line = letters.Length;
                    selectingOptions = false;
                    float offs = boxWidth - boxPadding * 2f;
                    if (optionsIndex == 0) {
                        index = (int) (cancelWidth / 2f / widestLetter);
                    } else if (optionsIndex == 1) {
                        index = (int) ((offs - beginWidth - backspaceWidth - spaceWidth / 2f - widestLetter * 2f) / widestLetter);
                    } else if (optionsIndex == 2) {
                        index = (int) ((offs - beginWidth - backspaceWidth / 2f - widestLetter) / widestLetter);
                    } else if (optionsIndex == 3) {
                        index = (int) ((offs - beginWidth / 2f) / widestLetter);
                    }
                }
                do {
                    line--;
                } while (line > 0 && (index >= letters[line].Length || letters[line][index] == ' '));
                while (index >= letters[line].Length || letters[line][index] == ' ') {
                    index--;
                }
                wiggler.Start();
                Audio.Play(SFX.ui_main_rename_entry_roll);

            } else if (Input.MenuConfirm.Pressed) {
                if (selectingOptions) {
                    if (optionsIndex == 0) {
                        Cancel();
                    } else if (optionsIndex == 1 && Value.Length > 0) {
                        Space();
                    } else if (optionsIndex == 2) {
                        Backspace();
                    } else if (optionsIndex == 3) {
                        Finish();
                    }
                } else if (Value.Length < MaxValueLength) {
                    Value += letters[line][index].ToString();
                    wiggler.Start();
                    Audio.Play(SFX.ui_main_rename_entry_char);
                } else {
                    Audio.Play(SFX.ui_main_button_invalid);
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

        private void Space() {
            if (Value.Length < MaxValueLength) {
                Value += " ";
                wiggler.Start();
                Audio.Play(SFX.ui_main_rename_entry_char);
            } else {
                Audio.Play(SFX.ui_main_button_invalid);
            }
        }

        private void Backspace() {
            if (Value.Length > 0) {
                Value = Value.Substring(0, Value.Length - 1);
                Audio.Play(SFX.ui_main_rename_entry_backspace);
            } else {
                Audio.Play(SFX.ui_main_button_invalid);
            }
        }

        private void Finish() {
            if (Value.Length >= MinValueLength) {
                Focused = false;
                OnExit?.Invoke(true);
                OnExit = null;
                Audio.Play(SFX.ui_main_rename_entry_accept);
            } else {
                Audio.Play(SFX.ui_main_button_invalid);
            }
        }

        private void Cancel() {
            Cancelled = true;
            Value = StartingValue;
            Focused = false;
            OnExit?.Invoke(false);
            OnExit = null;
            Audio.Play(SFX.ui_main_button_back);
        }

        public override void Render() {
            int prevIndex = index;
            // Only "focus" if we're not using the keyboard for input
            if (UseKeyboardInput)
                index = -1;

            // TODO: Rewrite or study and document the following code.
            // It stems from OuiFileNaming.

            Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.8f * ease);

            Vector2 pos = boxtopleft + new Vector2(boxPadding, boxPadding);

            int letterIndex = 0;
            foreach (string letter in letters) {
                for (int i = 0; i < letter.Length; i++) {
                    bool selected = letterIndex == line && i == index && !selectingOptions;
                    Vector2 scale = Vector2.One * (selected ? 1.2f : 1f);
                    Vector2 posLetter = pos + new Vector2(widestLetter, lineHeight) / 2f;
                    if (selected) {
                        posLetter += new Vector2(0f, wiggler.Value) * 8f;
                    }
                    DrawOptionText(letter[i].ToString(), posLetter, new Vector2(0.5f, 0.5f), scale, selected);
                    pos.X += widestLetter;
                }
                pos.X = boxtopleft.X + boxPadding;
                pos.Y += lineHeight + lineSpacing;
                letterIndex++;
            }
            float wiggle = wiggler.Value * 8f;

            pos.Y = boxtopleft.Y + boxHeight - lineHeight - boxPadding;
            Draw.Rect(pos.X, pos.Y - boxPadding * 0.5f, boxWidth - boxPadding * 2f, 4f, Color.White);

            DrawOptionText(cancel, pos + new Vector2(0f, lineHeight + ((selectingOptions && optionsIndex == 0) ? wiggle : 0f)), new Vector2(0f, 1f), Vector2.One * optionsScale, selectingOptions && optionsIndex == 0);
            pos.X = boxtopleft.X + boxWidth - backspaceWidth - widestLetter - spaceWidth - widestLetter - beginWidth - boxPadding;

            DrawOptionText(space, pos + new Vector2(0f, lineHeight + ((selectingOptions && optionsIndex == 1) ? wiggle : 0f)), new Vector2(0f, 1f), Vector2.One * optionsScale, selectingOptions && optionsIndex == 1, Value.Length == 0 || !Focused);
            pos.X += spaceWidth + widestLetter;

            DrawOptionText(backspace, pos + new Vector2(0f, lineHeight + ((selectingOptions && optionsIndex == 2) ? wiggle : 0f)), new Vector2(0f, 1f), Vector2.One * optionsScale, selectingOptions && optionsIndex == 2, Value.Length <= 0 || !Focused);
            pos.X += backspaceWidth + widestLetter;

            DrawOptionText(accept, pos + new Vector2(0f, lineHeight + ((selectingOptions && optionsIndex == 3) ? wiggle : 0f)), new Vector2(0f, 1f), Vector2.One * optionsScale, selectingOptions && optionsIndex == 3, Value.Length < 1 || !Focused);

            ActiveFont.DrawEdgeOutline(Value, Position + new Vector2(960f, 256f), new Vector2(0.5f, 0.5f), Vector2.One * 2f, Color.Gray, 4f, Color.DarkSlateBlue, 2f, Color.Black);

            index = prevIndex;
        }

        private void DrawOptionText(string text, Vector2 at, Vector2 justify, Vector2 scale, bool selected, bool disabled = false) {
            // Only draw "interactively" if not using the keyboard for input
            if (UseKeyboardInput) {
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
