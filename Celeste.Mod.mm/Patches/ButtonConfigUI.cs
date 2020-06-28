#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used
#pragma warning disable CS0414 // The field is assigned to, but never used
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste {
    public class patch_ButtonConfigUI : ButtonConfigUI {

        [MonoModIgnore]
        private enum Mappings {
            Jump,
            Dash,
            Grab,
            Talk,
            QuickRestart
        }

        private List<Buttons> all;
        protected List<Buttons> All => all;

        private bool remapping;

        private float remappingEase = 0f;

        private float inputDelay = 0f;

        private float timeout;

        private int currentlyRemapping;

        private bool closing;

        private float closingDelay;

        public extern void orig_ctor();
        [MonoModConstructor]
        public void ctor() {
            orig_ctor();

            OnESC = OnCancel = () => {
                Focused = false;
                closing = true;
                ForceRemapAll();
            };
        }

        /// <summary>
        /// ForceRemap all important mappings which are fully unassigned and require mappings when leaving the menu.
        /// </summary>
        protected virtual void ForceRemapAll() {
            if (Settings.Instance.BtnJump.Count <= 0)
                ForceRemap((int) Mappings.Jump);

            if (Settings.Instance.BtnDash.Count <= 0)
                ForceRemap((int) Mappings.Dash);

            if (Settings.Instance.BtnGrab.Count <= 0)
                ForceRemap((int) Mappings.Grab);

            if (Settings.Instance.BtnTalk.Count <= 0)
                ForceRemap((int) Mappings.Talk);
        }

        [MonoModIgnore]
        private extern string Label(Mappings mapping);

        /// <summary>
        /// Gets the label to display on-screen for a mapping.
        /// </summary>
        /// <param name="mapping">The mapping index</param>
        /// <returns>The button name to display</returns>
        protected virtual string GetLabel(int mapping) {
            // call the vanilla method for this.
            return Label((Mappings) mapping);
        }

        /// <summary>
        /// Adds a button mapping to the button config screen.
        /// </summary>
        /// <param name="btn">The mapping index</param>
        /// <param name="list">The list of buttons currently mapped to it</param>
        protected void AddButtonConfigLine(int btn, List<Buttons> list) {
            Add(new Setting(GetLabel(btn), list).Pressed(() => Remap(btn)).AltPressed(() => ClearRemap(btn)));
        }

        /// <summary>
        /// Adds a button mapping to the button config screen.
        /// </summary>
        /// <param name="btn">The mapping (should be an enum value)</param>
        /// <param name="list">The list of buttons currently mapped to it</param>
        protected void AddButtonConfigLine<T>(T btn, List<Buttons> list) where T : Enum {
            AddButtonConfigLine(btn.GetHashCode(), list);
        }

        /// <summary>
        /// Forces a button to be bound to an action, in addition to the already bound buttons.
        /// </summary>
        /// <param name="defaultBtn">The button to force bind</param>
        /// <param name="boundBtn">The button already bound</param>
        /// <returns>A list containing both button and defaultBtn</returns>
        protected List<Buttons> ForceDefaultButton(Buttons defaultBtn, Buttons boundBtn) {
            List<Buttons> list = new List<Buttons> { boundBtn };
            if (boundBtn != defaultBtn)
                list.Add(defaultBtn);
            return list;
        }

        /// <summary>
        /// Forces a button to be bound to an action, in addition to already bound buttons.
        /// </summary>
        /// <param name="defaultBtn">The button to force bind</param>
        /// <param name="boundBtns">The list of buttons already bound</param>
        /// <returns>A list containing both buttons in list and defaultBtn</returns>
        protected List<Buttons> ForceDefaultButton(Buttons defaultBtn, List<Buttons> boundBtns) {
            if (!boundBtns.Contains(defaultBtn))
                boundBtns.Add(defaultBtn);
            return boundBtns;
        }

        /// <summary>
        /// Rebuilds the button mapping menu. Should clear the menu and add back all options.
        /// </summary>
        /// <param name="index">The index to focus on in the menu</param>
        [MonoModReplace]
        public virtual void Reload(int index = -1) {
            Clear();
            Add(new Header(Dialog.Clean("BTN_CONFIG_TITLE")));
            Add(new Info());

            AddButtonConfigLine(Mappings.Jump, Settings.Instance.BtnJump);
            AddButtonConfigLine(Mappings.Dash, Settings.Instance.BtnDash);
            AddButtonConfigLine(Mappings.Grab, Settings.Instance.BtnGrab);
            AddButtonConfigLine(Mappings.Talk, Settings.Instance.BtnTalk);
            AddButtonConfigLine(Mappings.QuickRestart, Settings.Instance.BtnAltQuickRestart);

            Add(new SubHeader(""));
            Add(new Button(Dialog.Clean("KEY_CONFIG_RESET")) {
                IncludeWidthInMeasurement = false,
                AlwaysCenter = true,
                OnPressed = () => {
                    Settings.Instance.SetDefaultButtonControls(reset: true);
                    Input.Initialize();
                    Reload(Selection);
                }
            });
            if (index >= 0) {
                Selection = index;
            }
        }

        [MonoModReplace]
        private void Remap(int mapping) {
            if (Input.GuiInputController()) {
                remapping = true;
                currentlyRemapping = mapping;
                timeout = 5f;
                Focused = false;
            }
        }

        private void ClearRemap(int mapping) {
            GetRemapList(mapping, 0)?.Clear();
            Input.Initialize();
            Reload(Selection);
        }

        [MonoModReplace]
        private void AddRemap(Buttons btn) {
            remapping = false;
            inputDelay = 0.25f;
            List<Buttons> btnList = GetRemapList(currentlyRemapping, btn);
            if (!btnList.Contains(btn)) {
                if (btnList.Count >= 4) {
                    btnList.RemoveAt(0);
                }
                btnList.Add(btn);
                RemoveDuplicates(currentlyRemapping, btnList, btn);
            }
            Input.Initialize();
            Reload(Selection);
        }

        /// <summary>
        /// Removes the button from all lists other than the current remapping list if needed.
        /// </summary>
        /// <param name="remapping">The int value of the mapping being remapped</param>
        /// <param name="list">The list that newBtn has been added to</param>
        /// <param name="btn">The new button that the user is attempting to set.</param>
        protected virtual void RemoveDuplicates(int remapping, List<Buttons> list, Buttons btn) {
            if (list != Settings.Instance.BtnJump)
                Settings.Instance.BtnJump.Remove(btn);

            if (list != Settings.Instance.BtnDash && list != Settings.Instance.BtnTalk)
                Settings.Instance.BtnDash.Remove(btn);

            if (list != Settings.Instance.BtnGrab)
                Settings.Instance.BtnGrab.Remove(btn);

            if (list != Settings.Instance.BtnTalk && list != Settings.Instance.BtnDash)
                Settings.Instance.BtnTalk.Remove(btn);

            if (list != Settings.Instance.BtnAltQuickRestart)
                Settings.Instance.BtnAltQuickRestart.Remove(btn);
        }

        /// <summary>
        /// Forcibly gives all important mappings some default button values.
        /// </summary>
        /// <param name="mapping">The int value of the mapping being remapped</param>
        protected virtual void ForceRemap(int mapping) {
            List<Buttons> left = new List<Buttons>(All);

            foreach (Buttons btn in Settings.Instance.BtnJump)
                left.Remove(btn);

            foreach (Buttons btn in Settings.Instance.BtnDash)
                left.Remove(btn);

            foreach (Buttons btn in Settings.Instance.BtnGrab)
                left.Remove(btn);

            foreach (Buttons btn in Settings.Instance.BtnTalk)
                left.Remove(btn);

            foreach (Buttons btn in Settings.Instance.BtnAltQuickRestart)
                left.Remove(btn);

            currentlyRemapping = mapping;

            if (left.Count > 0) {
                AddRemap(left[0]);
            } else {
                Buttons button;
                if (TrySteal(Settings.Instance.BtnJump, out button)) {
                    AddRemap(button);
                } else if (TrySteal(Settings.Instance.BtnDash, out button)) {
                    AddRemap(button);
                } else if (TrySteal(Settings.Instance.BtnGrab, out button)) {
                    AddRemap(button);
                } else if (TrySteal(Settings.Instance.BtnTalk, out button)) {
                    AddRemap(button);
                } else if (TrySteal(Settings.Instance.BtnAltQuickRestart, out button)) {
                    AddRemap(button);
                }
            }

            closingDelay = 0.5f;
        }

        // Replace to make the method protected.
        [MonoModReplace]
        protected bool TrySteal(List<Buttons> list, out Buttons button) {
            if (list.Count > 1) {
                button = list[0];
                list.RemoveAt(0);
                return true;
            }

            button = Buttons.A;
            return false;
        }

        /// <summary>
        /// Returns the list used to remap buttons during a remap operation.
        /// This should be the a List<Buttons> field in your settings class
        /// </summary>
        /// <param name="remapping">The int value of the mapping being remapped</param>
        /// <param name="newBtn">The new button that the user is attempting to set.</param>
        /// <returns>the field to set buttons with, otherwise return null to cancel the operation</returns>
        protected virtual List<Buttons> GetRemapList(int remapping, Buttons newBtn) {
            Mappings mappedBtn = (Mappings) remapping;

            switch (mappedBtn) {
                case Mappings.Jump:
                    return Settings.Instance.BtnJump;

                case Mappings.Dash:
                    return Settings.Instance.BtnDash;

                case Mappings.Grab:
                    return Settings.Instance.BtnGrab;

                case Mappings.Talk:
                    return Settings.Instance.BtnTalk;

                case Mappings.QuickRestart:
                    return Settings.Instance.BtnAltQuickRestart;

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
            Vector2 center = new Vector2(1920f, 1080f) * 0.5f;
            if (Input.GuiInputController()) {
                RenderTextMenu();
                if (remappingEase > 0f) {
                    Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.95f * Ease.CubeInOut(remappingEase));
                    ActiveFont.Draw(Dialog.Get("BTN_CONFIG_CHANGING"), center + new Vector2(0f, -8f), new Vector2(0.5f, 1f), Vector2.One * 0.7f, Color.LightGray * Ease.CubeIn(remappingEase));
                    ActiveFont.Draw(GetLabel(currentlyRemapping), center + new Vector2(0f, 8f), new Vector2(0.5f, 0f), Vector2.One * 2f, Color.White * Ease.CubeIn(remappingEase));
                }
            } else {
                ActiveFont.Draw(Dialog.Clean("BTN_CONFIG_NOCONTROLLER"), center, new Vector2(0.5f, 0.5f), Vector2.One, Color.White * Ease.CubeOut(Alpha));
            }
        }
    }
}
