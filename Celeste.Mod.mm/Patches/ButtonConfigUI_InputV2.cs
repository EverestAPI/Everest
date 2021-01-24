#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0414 // The field is assigned to, but never used
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;

namespace Celeste {
    [MonoModIfFlag("V2:Input")]
    [MonoModPatch("ButtonConfigUI")]
    public class patch_ButtonConfigUI_InputV2 : ButtonConfigUI {

        private List<Buttons> all;
        protected List<Buttons> All => all;

        [MonoModIgnore]
        private bool remapping;
        [MonoModIgnore]
        private Binding remappingBinding;
        [MonoModIgnore]
        private string remappingText;
        [MonoModIgnore]
        private float timeout;

        public extern void orig_ctor();
        [MonoModConstructor]
        public void ctor() {
            orig_ctor();
            Reload();
        }

        /// <summary>
        /// ForceRemap all important mappings which are fully unassigned and require mappings when leaving the menu.
        /// </summary>
        [Obsolete("This method exists so that older mods can still be loaded but is no longer used.")]
        protected virtual void ForceRemapAll() {
        }

        /// <summary>
        /// Gets the label to display on-screen for a mapping.
        /// </summary>
        /// <param name="mapping">The mapping index</param>
        /// <returns>The button name to display</returns>
        [Obsolete("This method exists so that older mods can still be loaded but is no longer used.")]
        protected virtual string GetLabel(int mapping) {
            return "KEY_CONFIG_USING_OBSOLETE_API";
        }

        /// <summary>
        /// Adds a button mapping to the button config screen.
        /// </summary>
        /// <param name="btn">The mapping index</param>
        /// <param name="list">The list of buttons currently mapped to it</param>
        [Obsolete("This method exists so that older mods can still be loaded but should no longer be used.")]
        protected void AddButtonConfigLine(int btn, List<Buttons> list) {
            Add(new patch_TextMenu.patch_Setting(GetLabel(btn), list).Pressed(() => Remap(btn)).AltPressed(() => {
                GetRemapList(btn, (Buttons) (-1))?.Clear();
                Input.Initialize();
                Reload(Selection);
            }));
        }

        /// <summary>
        /// Adds a button mapping to the button config screen.
        /// </summary>
        /// <param name="btn">The mapping (should be an enum value)</param>
        /// <param name="list">The list of buttons currently mapped to it</param>
        [Obsolete("This method exists so that older mods can still be loaded but should no longer be used.")]
        protected void AddButtonConfigLine<T>(T btn, List<Buttons> list) where T : Enum {
            AddButtonConfigLine(btn.GetHashCode(), list);
        }

        /// <summary>
        /// Forces a button to be bound to an action, in addition to the already bound buttons.
        /// </summary>
        /// <param name="defaultBtn">The button to force bind</param>
        /// <param name="boundBtn">The button already bound</param>
        /// <returns>A list containing both button and defaultBtn</returns>
        [Obsolete("This method exists so that older mods can still be loaded but should no longer be used.")]
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
        [Obsolete("This method exists so that older mods can still be loaded but should no longer be used.")]
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
            Add(new InputMappingInfo(true));

            Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("KEY_CONFIG_MOVEMENT")));
            AddMap("LEFT", Settings.Instance.Left);
            AddMap("RIGHT", Settings.Instance.Right);
            AddMap("UP", Settings.Instance.Up);
            AddMap("DOWN", Settings.Instance.Down);

            Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("KEY_CONFIG_GAMEPLAY")));
            AddMap("JUMP", Settings.Instance.Jump);
            AddMap("DASH", Settings.Instance.Dash);
            AddMap("GRAB", Settings.Instance.Grab);
            AddMap("TALK", Settings.Instance.Talk);

            Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("KEY_CONFIG_MENUS")));
            AddMap("LEFT", Settings.Instance.MenuLeft);
            AddMap("RIGHT", Settings.Instance.MenuRight);
            AddMap("UP", Settings.Instance.MenuUp);
            AddMap("DOWN", Settings.Instance.MenuDown);
            AddMap("CONFIRM", Settings.Instance.Confirm);
            AddMap("CANCEL", Settings.Instance.Cancel);
            AddMap("JOURNAL", Settings.Instance.Journal);
            AddMap("PAUSE", Settings.Instance.Pause);
            AddMap("QUICKRESTART", Settings.Instance.QuickRestart);

            Add(new patch_TextMenu.patch_SubHeader(Dialog.Clean("KEY_CONFIG_ADVANCED")));
            AddMap("DEMODASH", Settings.Instance.DemoDash);

            Add(new patch_TextMenu.patch_SubHeader(""));
            Add(new Button(Dialog.Clean("KEY_CONFIG_RESET")) {
                IncludeWidthInMeasurement = false,
                AlwaysCenter = true,
                OnPressed = () => {
                    patch_Settings_InputV1.Instance.SetDefaultButtonControls(reset: true);
                    Input.Initialize();
                    Reload(Selection);
                }
            });

            if (index >= 0) {
                Selection = index;
            }
        }

        [MonoModIgnore]
        [MakeMethodPublic]
        public extern void AddMap(string label, Binding binding);

        public void AddMapForceLabel(string label, Binding binding) {
            // FIXME!!! MonoMod likes to patch nested hidden compiler generated delegate class even if this parent class isn't.
            object _binding = binding;
            Add(new Setting(label, binding, true).Pressed(() => {
                remappingText = label;
                Remap(_binding);
            }).AltPressed(() => {
                ClearRemap(_binding);
            }));
        }

        // FIXME!!! MonoMod likes to patch nested hidden compiler generated delegate class even if this parent class isn't.
        private void Remap(object binding) => Remap((Binding) binding);
        private void ClearRemap(object binding) => ClearRemap((Binding) binding);

        [MonoModIgnore]
        [MakeMethodPublic]
        public extern void Remap(Binding binding);

        [MonoModIgnore]
        [MakeMethodPublic]
        public extern void ClearRemap(Binding binding);

        [MonoModIgnore]
        [MakeMethodPublic]
        public extern void AddRemap(Keys key);

        [Obsolete("This method exists so that older mods can still be loaded but should no longer be used.")]
        private void Remap(int mapping) {
            if (Input.GuiInputController()) {
                List<Buttons> btns = GetRemapList(mapping, (Buttons) (-1));
                if (btns == null)
                    throw new Exception($"{GetType().FullName} is using the old input system (before Celeste 1.3.3.12) and Everest can't bridge the gap for this mod.");

                remapping = true;
                remappingBinding = new Binding() {
                    Controller = btns
                };
                remappingText = GetLabel(mapping);
                timeout = 5f;
                Focused = false;
            }
        }

        /// <summary>
        /// Removes the button from all lists other than the current remapping list if needed.
        /// </summary>
        /// <param name="remapping">The int value of the mapping being remapped</param>
        /// <param name="list">The list that newBtn has been added to</param>
        /// <param name="btn">The new button that the user is attempting to set.</param>
        [Obsolete("This method exists so that older mods can still be loaded but is no longer used.")]
        protected virtual void RemoveDuplicates(int remapping, List<Buttons> list, Buttons btn) {
        }

        /// <summary>
        /// Forcibly gives all important mappings some default button values.
        /// </summary>
        /// <param name="mapping">The int value of the mapping being remapped</param>
        [Obsolete("This method exists so that older mods can still be loaded but is no longer used.")]
        protected virtual void ForceRemap(int mapping) {
        }

        [Obsolete("This method exists so that older mods can still be loaded but is no longer used.")]
        protected bool TrySteal(List<Buttons> list, out Buttons button) {
            button = (Buttons) (-1);
            return false;
        }

        /// <summary>
        /// Returns the list used to remap buttons during a remap operation.
        /// This should be the a List&lt;Buttons&gt; field in your settings class
        /// </summary>
        /// <param name="remapping">The int value of the mapping being remapped</param>
        /// <param name="newBtn">The new button that the user is attempting to set.</param>
        /// <returns>the field to set buttons with, otherwise return null to cancel the operation</returns>
        [Obsolete("This method exists so that older mods can still be loaded but should no longer be used.")]
        protected virtual List<Buttons> GetRemapList(int remapping, Buttons newBtn) {
            return null;
        }

    }
}
