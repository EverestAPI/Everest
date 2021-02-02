#pragma warning disable CS0169 // The field is never used
#pragma warning disable CS0414 // The field is assigned to, but never used
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste {
    [MonoModIfFlag("V1:Input")]
    [MonoModPatch("KeyboardConfigUI")]
    public class patch_KeyboardConfigUI_InputV1 : KeyboardConfigUI {

        [MonoModIgnore]
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
            QuickRestart,
            DemoDash
        }

        private bool remapping;

        private float remappingEase = 0f;

        private float inputDelay = 0f;

        private float timeout;

        private int currentlyRemapping;

        private bool closing;

        private bool additiveRemap;

        [MonoModIgnore]
        private extern string Label(Mappings mapping);

        /// <summary>
        /// Gets the label to display on-screen for a mapping.
        /// </summary>
        /// <param name="mapping">The mapping index</param>
        /// <returns>The key name to display</returns>
        protected virtual string GetLabel(int mapping) {
            // call the vanilla method for this.
            return Label((Mappings) mapping);
        }

        /// <summary>
        /// Adds a key mapping to the keyboard config screen.
        /// </summary>
        /// <param name="key">The mapping index</param>
        /// <param name="list">The list of keys currently mapped to it</param>
        protected void AddKeyConfigLine(int key, List<Keys> list) {
            Add(new patch_TextMenu.patch_Setting(GetLabel(key), list).Pressed(() => {
                Remap(key);
            }));
        }

        /// <summary>
        /// Adds a key mapping to the keyboard config screen.
        /// </summary>
        /// <param name="key">The mapping (should be an enum value)</param>
        /// <param name="list">The list of keys currently mapped to it</param>
        protected void AddKeyConfigLine<T>(T key, List<Keys> list) where T : Enum {
            AddKeyConfigLine(key.GetHashCode(), list);
        }

        /// <summary>
        /// Forces a key to be bound to an action, in addition to the already bound key.
        /// </summary>
        /// <param name="defaultKey">The key to force bind</param>
        /// <param name="boundKey">The key already bound</param>
        /// <returns>A list containing both key and defaultKey</returns>
        protected List<Keys> ForceDefaultKey(Keys defaultKey, Keys boundKey) {
            List<Keys> list = new List<Keys> { boundKey };
            if (boundKey != defaultKey)
                list.Add(defaultKey);
            return list;
        }

        /// <summary>
        /// Forces a key to be bound to an action, in addition to already bound keys.
        /// </summary>
        /// <param name="defaultKey">The key to force bind</param>
        /// <param name="boundKeys">The list of keys already bound</param>
        /// <returns>A list containing both keys in list and defaultKey</returns>
        protected List<Keys> ForceDefaultKey(Keys defaultKey, List<Keys> boundKeys) {
            if (!boundKeys.Contains(defaultKey))
                boundKeys.Add(defaultKey);
            return boundKeys;
        }

        /// <summary>
        /// Rebuilds the key mapping menu. Should clear the menu and add back all options.
        /// </summary>
        /// <param name="index">The index to focus on in the menu</param>
        [MonoModReplace]
        public virtual void Reload(int index = -1) {
            Clear();
            Add(new Header(Dialog.Clean("KEY_CONFIG_TITLE")));
            Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("KEY_CONFIG_ADDITION_HINT")));

            Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("KEY_CONFIG_MOVEMENT")));
            AddKeyConfigLine(Mappings.Left, ForceDefaultKey(Keys.Left, patch_Settings_InputV1.Instance.Left));
            AddKeyConfigLine(Mappings.Right, ForceDefaultKey(Keys.Right, patch_Settings_InputV1.Instance.Right));
            AddKeyConfigLine(Mappings.Up, ForceDefaultKey(Keys.Up, patch_Settings_InputV1.Instance.Up));
            AddKeyConfigLine(Mappings.Down, ForceDefaultKey(Keys.Down, patch_Settings_InputV1.Instance.Down));

            Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("KEY_CONFIG_GAMEPLAY")));
            AddKeyConfigLine(Mappings.Jump, patch_Settings_InputV1.Instance.Jump);
            AddKeyConfigLine(Mappings.Dash, patch_Settings_InputV1.Instance.Dash);
            AddKeyConfigLine(Mappings.Grab, patch_Settings_InputV1.Instance.Grab);
            AddKeyConfigLine(Mappings.Talk, patch_Settings_InputV1.Instance.Talk);

            Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("KEY_CONFIG_MENUS")));
            AddKeyConfigLine(Mappings.Confirm, ForceDefaultKey(Keys.Enter, patch_Settings_InputV1.Instance.Confirm));
            AddKeyConfigLine(Mappings.Cancel, ForceDefaultKey(Keys.Back, patch_Settings_InputV1.Instance.Cancel));
            AddKeyConfigLine(Mappings.Pause, ForceDefaultKey(Keys.Escape, patch_Settings_InputV1.Instance.Pause));
            AddKeyConfigLine(Mappings.Journal, patch_Settings_InputV1.Instance.Journal);
            AddKeyConfigLine(Mappings.QuickRestart, patch_Settings_InputV1.Instance.QuickRestart);

            AddDemoDashLine();

            Add(new patch_TextMenu.patch_SubHeader(""));
            Button button = new Button(Dialog.Clean("KEY_CONFIG_RESET"));
            button.IncludeWidthInMeasurement = false;
            button.AlwaysCenter = true;
            button.OnPressed = delegate {
                patch_Settings_InputV1.Instance.SetDefaultKeyboardControls(reset: true);
                Input.Initialize();
                Reload(Selection);
            };
            Add(button);
            if (index >= 0) {
                Selection = index;
            }
        }

        // Celeste 1.3.3.11 exposes a DemoDash mapping.
        [MonoModIgnore]
        private extern void AddDemoDashLine();

        [MonoModIfFlag("Lacks:RevealDemoConfig")]
        [MonoModPatch("AddDemoDashLine")]
        [MonoModReplace]
        private void AddDemoDashLineStub() {
        }

        [MonoModIfFlag("Has:RevealDemoConfig")]
        [MonoModPatch("AddDemoDashLine")]
        [MonoModReplace]
        private void AddDemoDashLineImpl() {
            if (patch_Settings_InputV1.Instance.RevealDemoConfig) {
                Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("KEY_CONFIG_ADVANCED")));
                AddKeyConfigLine(Mappings.DemoDash, patch_Settings_InputV1.Instance.DemoDash);
            }
        }

        // these keys only support a single mapping (they are not lists in the settings file).
        private static Mappings[] keysWithSingleBindings = { Mappings.Left, Mappings.Right, Mappings.Up, Mappings.Down };

        private void Remap(int mapping) {
            remapping = true;
            currentlyRemapping = mapping;
            timeout = 5f;
            Focused = false;
            KeyboardState keyboard = Keyboard.GetState();
            additiveRemap = (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
                && SupportsMultipleBindings(mapping);
        }

        /// <summary>
        /// Determines if the key being set supports multiple bindings.
        /// If this is the case, Shift+Confirm will allow to add a binding and Confirm will replace existing bindings.
        /// </summary>
        /// <param name="mapping">The mapping</param>
        /// <returns>true if the key supports multiple bindings, false otherwise</returns>
        protected virtual bool SupportsMultipleBindings(int mapping) {
            return !keysWithSingleBindings.Contains((Mappings) mapping);
        }

        [MonoModReplace]
        private void SetRemap(Keys key) {
            remapping = false;
            inputDelay = 0.25f;
            List<Keys> keyList = GetRemapList(currentlyRemapping, key);
            if (keyList != null) {
                if (!additiveRemap)
                    keyList.Clear();
                if (!keyList.Contains(key)) {
                    keyList.Add(key);
                } else if (keyList.Count >= 2) {
                    keyList.Remove(key);
                }
            }
            Input.Initialize();
            Reload(Selection);
        }

        /// <summary>
        /// Returns the list used to remap keys during a remap operation.
        /// This should be a List&lt;Keys&gt; field in your settings class
        /// </summary>
        /// <param name="remapping">The int value of the mapping being remapped</param>
        /// <param name="newKey">The new key that the user is attempting to set.</param>
        /// <returns>the field to set keys with, otherwise return null to cancel the operation</returns>
        protected virtual List<Keys> GetRemapList(int remapping, Keys newKey) {
            Mappings mappedKey = (Mappings) remapping;
            if (newKey == Keys.None ||
                (newKey == Keys.Left && mappedKey != Mappings.Left) ||
                (newKey == Keys.Right && mappedKey != Mappings.Right) ||
                (newKey == Keys.Up && mappedKey != Mappings.Up) ||
                (newKey == Keys.Down && mappedKey != Mappings.Down) ||
                (newKey == Keys.Enter && mappedKey != Mappings.Confirm) ||
                (newKey == Keys.Back && mappedKey != Mappings.Cancel)) {
                return null;
            }

            switch (mappedKey) {
                case Mappings.Left:
                    patch_Settings_InputV1.Instance.Left = ((newKey != Keys.Left) ? newKey : Keys.None);
                    return null;

                case Mappings.Right:
                    patch_Settings_InputV1.Instance.Right = ((newKey != Keys.Right) ? newKey : Keys.None);
                    return null;

                case Mappings.Up:
                    patch_Settings_InputV1.Instance.Up = ((newKey != Keys.Up) ? newKey : Keys.None);
                    return null;

                case Mappings.Down:
                    patch_Settings_InputV1.Instance.Down = ((newKey != Keys.Down) ? newKey : Keys.None);
                    return null;

                case Mappings.Jump:
                    return patch_Settings_InputV1.Instance.Jump;

                case Mappings.Dash:
                    return patch_Settings_InputV1.Instance.Dash;

                case Mappings.Grab:
                    return patch_Settings_InputV1.Instance.Grab;

                case Mappings.Talk:
                    return patch_Settings_InputV1.Instance.Talk;

                case Mappings.Confirm:
                    if (!patch_Settings_InputV1.Instance.Cancel.Contains(newKey) && !patch_Settings_InputV1.Instance.Pause.Contains(newKey)) {
                        if (newKey != Keys.Enter) {
                            return patch_Settings_InputV1.Instance.Confirm;
                        }
                    }
                    return null;

                case Mappings.Cancel:
                    if (!patch_Settings_InputV1.Instance.Confirm.Contains(newKey) && !patch_Settings_InputV1.Instance.Pause.Contains(newKey)) {
                        if (newKey != Keys.Back) {
                            return patch_Settings_InputV1.Instance.Cancel;
                        }
                    }
                    return null;

                case Mappings.Pause:
                    if (!patch_Settings_InputV1.Instance.Confirm.Contains(newKey) && !patch_Settings_InputV1.Instance.Cancel.Contains(newKey)) {
                        return patch_Settings_InputV1.Instance.Pause;
                    }
                    return null;

                case Mappings.Journal:
                    return patch_Settings_InputV1.Instance.Journal;

                case Mappings.QuickRestart:
                    return patch_Settings_InputV1.Instance.QuickRestart;

                default:
                    return null;
            }
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
                ActiveFont.Draw(GetLabel(currentlyRemapping), value + new Vector2(0f, 8f), new Vector2(0.5f, 0f), Vector2.One * 2f, Color.White * Ease.CubeIn(remappingEase));
            }
        }
    }
}
