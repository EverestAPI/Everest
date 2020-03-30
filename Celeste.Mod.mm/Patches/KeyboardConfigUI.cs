#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used
#pragma warning disable CS0414 // The field is assigned to, but never used
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;

namespace Celeste {
    public class patch_KeyboardConfigUI : KeyboardConfigUI {

        private enum Mappings {
            Left,
            Right,
            Up,
            Down,
            Jump,
            Dash,
            Grab,
            Talk,
            Confirm,
            Cancel,
            Pause,
            Journal,
            QuickRestart
        }

        private bool remapping;

        private float remappingEase = 0f;

        private float inputDelay = 0f;

        private float timeout;

        private Mappings remappingKey;

        private bool closing;

        private bool additiveRemap;

        [MonoModIgnore]
        private extern string Label(Mappings mapping);

        private void addKeyConfigLine(Mappings key, List<Keys> list) {
            Add(new Setting(Label(key), list).Pressed(delegate { Remap(key); }));
        }

        private List<Keys> forceDefaultKey(Keys defaultKey, Keys key) {
            List<Keys> list = new List<Keys> { key };
            if (key != defaultKey)
                list.Add(defaultKey);
            return list;
        }

        private List<Keys> forceDefaultKey(Keys defaultKey, List<Keys> list) {
            if (!list.Contains(defaultKey))
                list.Add(defaultKey);
            return list;
        }

        [MonoModReplace]
        public void Reload(int index = -1) {
            Clear();
            Add(new Header(Dialog.Clean("KEY_CONFIG_TITLE")));
            Add(new SubHeader(Dialog.Clean("KEY_CONFIG_ADDITION_HINT")));

            Add(new SubHeader(Dialog.Clean("KEY_CONFIG_MOVEMENT")));
            addKeyConfigLine(Mappings.Left, forceDefaultKey(Keys.Left, Settings.Instance.Left));
            addKeyConfigLine(Mappings.Right, forceDefaultKey(Keys.Right, Settings.Instance.Right));
            addKeyConfigLine(Mappings.Up, forceDefaultKey(Keys.Up, Settings.Instance.Up));
            addKeyConfigLine(Mappings.Down, forceDefaultKey(Keys.Down, Settings.Instance.Down));

            Add(new SubHeader(Dialog.Clean("KEY_CONFIG_GAMEPLAY")));
            addKeyConfigLine(Mappings.Jump, Settings.Instance.Jump);
            addKeyConfigLine(Mappings.Dash, Settings.Instance.Dash);
            addKeyConfigLine(Mappings.Grab, Settings.Instance.Grab);
            addKeyConfigLine(Mappings.Talk, Settings.Instance.Talk);

            Add(new SubHeader(Dialog.Clean("KEY_CONFIG_MENUS")));
            addKeyConfigLine(Mappings.Confirm, forceDefaultKey(Keys.Enter, Settings.Instance.Confirm));
            addKeyConfigLine(Mappings.Cancel, forceDefaultKey(Keys.Back, Settings.Instance.Cancel));
            addKeyConfigLine(Mappings.Pause, forceDefaultKey(Keys.Escape, Settings.Instance.Pause));
            addKeyConfigLine(Mappings.Journal, Settings.Instance.Journal);
            addKeyConfigLine(Mappings.QuickRestart, Settings.Instance.QuickRestart);
            Add(new SubHeader(""));

            Button button = new Button(Dialog.Clean("KEY_CONFIG_RESET"));
            button.IncludeWidthInMeasurement = false;
            button.AlwaysCenter = true;
            button.OnPressed = delegate {
                Settings.Instance.SetDefaultKeyboardControls(reset: true);
                Input.Initialize();
                Reload(Selection);
            };
            Add(button);
            if (index >= 0) {
                Selection = index;
            }
        }

        private extern void orig_Remap(Mappings mapping);
        private void Remap(Mappings mapping) {
            orig_Remap(mapping);
            KeyboardState keyboard = Keyboard.GetState();
            additiveRemap = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        }

        [MonoModReplace]
        private void SetRemap(Keys key) {
            remapping = false;
            inputDelay = 0.25f;
            if (key == Keys.None || 
                (key == Keys.Left && remappingKey != Mappings.Left) || 
                (key == Keys.Right && remappingKey != Mappings.Right) ||
                (key == Keys.Up && remappingKey != Mappings.Up) || 
                (key == Keys.Down && remappingKey != Mappings.Down) || 
                (key == Keys.Enter && remappingKey != Mappings.Confirm) || 
                (key == Keys.Back && remappingKey != Mappings.Cancel)) {
                return;
            }
            List<Keys> keyList = null;
            switch (remappingKey) {
                case Mappings.Left:
                    Settings.Instance.Left = ((key != Keys.Left) ? key : Keys.None);
                    break;
                case Mappings.Right:
                    Settings.Instance.Right = ((key != Keys.Right) ? key : Keys.None);
                    break;
                case Mappings.Up:
                    Settings.Instance.Up = ((key != Keys.Up) ? key : Keys.None);
                    break;
                case Mappings.Down:
                    Settings.Instance.Down = ((key != Keys.Down) ? key : Keys.None);
                    break;
                case Mappings.Jump:
                    keyList = Settings.Instance.Jump;
                    break;
                case Mappings.Dash:
                    keyList = Settings.Instance.Dash;
                    break;
                case Mappings.Grab:
                    keyList = Settings.Instance.Grab;
                    break;
                case Mappings.Talk:
                    keyList = Settings.Instance.Talk;
                    break;
                case Mappings.Confirm:
                    if (!Settings.Instance.Cancel.Contains(key) && !Settings.Instance.Pause.Contains(key)) {
                        if (key != Keys.Enter) {
                            keyList = Settings.Instance.Confirm;
                        }
                    }
                    break;
                case Mappings.Cancel:
                    if (!Settings.Instance.Confirm.Contains(key) && !Settings.Instance.Pause.Contains(key)) {
                        if (key != Keys.Back) {
                            keyList = Settings.Instance.Cancel;
                        }
                    }
                    break;
                case Mappings.Pause:
                    if (!Settings.Instance.Confirm.Contains(key) && !Settings.Instance.Cancel.Contains(key)) {
                        keyList = Settings.Instance.Pause;
                    }
                    break;
                case Mappings.Journal:
                    keyList = Settings.Instance.Journal;
                    break;
                case Mappings.QuickRestart:
                    keyList = Settings.Instance.QuickRestart;
                    break;
            }
            if (keyList != null) {
                if (!additiveRemap)
                    keyList.Clear();
                if (!keyList.Contains(key))
                    keyList.Add(key);
            }
            Input.Initialize();
            Reload(Selection);
        }

        [MonoModLinkTo("Celeste.TextMenu", "System.Void Render()")]
        [MonoModRemove]
        private extern void RenderTextMenu();

        [MonoModReplace]
        public override void Render() {
            Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * Ease.CubeOut(Alpha));
            RenderTextMenu();
            if (remappingEase > 0f) {
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.95f * Ease.CubeInOut(remappingEase));
                Vector2 value = new Vector2(1920f, 1080f) * 0.5f;
                ActiveFont.Draw(additiveRemap ? Dialog.Get("KEY_CONFIG_ADDING") : Dialog.Get("KEY_CONFIG_CHANGING"), value + new Vector2(0f, -8f), new Vector2(0.5f, 1f), Vector2.One * 0.7f, Color.LightGray * Ease.CubeIn(remappingEase));
                ActiveFont.Draw(Label(remappingKey), value + new Vector2(0f, 8f), new Vector2(0.5f, 0f), Vector2.One * 2f, Color.White * Ease.CubeIn(remappingEase));
            }
        }
    }
}
