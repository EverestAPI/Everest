using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;

namespace Celeste.Mod {
    /// <summary>
    /// A wrapper class for a custom <see cref="VirtualButton"/>.
    /// <br></br>
    /// Default buttons can be set using <see cref="DefaultButtonBindingAttribute"/>.
    /// <br></br>
    /// <see href="https://github.com/EverestAPI/Resources/wiki/Mod-Settings#ButtonBinding">Read More</see>
    /// </summary>
    public class ButtonBinding {

        public List<Buttons> Buttons {
            get => Binding.Controller;
            set => Binding.Controller = value ?? new List<Buttons>();
        }

        public List<Keys> Keys {
            get => Binding.Keyboard;
            set => Binding.Keyboard = value ?? new List<Keys>();
        }

        public List<patch_MInput.patch_MouseData.MouseButtons> MouseButtons {
            get => ((patch_Binding) Binding).Mouse;
            set => ((patch_Binding) Binding).Mouse = value ?? new List<patch_MInput.patch_MouseData.MouseButtons>();
        }

        private Binding _Binding;

        [YamlIgnore] // Binding uses XmlIgnores and migrating old mappings is hard.
        public Binding Binding {
            get => _Binding;
            private set => _Binding = value;
        }

        #region VirtualButton Members

        [YamlIgnore]
        public bool Check => Button?.Check ?? false;
        [YamlIgnore]
        public bool Pressed => Button?.Pressed ?? false;
        [YamlIgnore]
        public bool Released => Button?.Released ?? false;
        [YamlIgnore]
        public bool Repeating => Button?.Repeating ?? false;

        [YamlIgnore]
        [Obsolete("Please move to Binding ASAP.")]
        public List<patch_VirtualButton.Node> Nodes {
            get => Button?.Nodes;
            set {
                //No null-coalescing assignment operator in C#7.3
                if (Button != null)
                    Button.Nodes = value;
            }
        }

        [YamlIgnore]
        public float BufferTime {
            get => Button?.BufferTime ?? default;
            set {
                if (Button != null)
                    Button.BufferTime = value;
            }
        }
        [YamlIgnore]
        public Keys? DebugOverridePressed {
            get => Button?.DebugOverridePressed;
            set {
                if (Button != null)
                    Button.DebugOverridePressed = value;
            }
        }

        #endregion

        [YamlIgnore]
        public patch_VirtualButton Button;

        public ButtonBinding()
            : this(0) {
        }

        public ButtonBinding(Buttons buttons, params Keys[] keys) {
            Init(buttons, keys);
        }

        [MonoModReplace]
        private void Init(Buttons buttons, params Keys[] keys) {
            Binding = new Binding() {
                Controller = Enum.GetValues(typeof(Buttons)).Cast<Buttons>().Where(b => (buttons & b) == b).ToList(),
                Keyboard = new List<Keys>(keys)
            };
        }


        #region VirtualButton Members

        /// <inheritdoc cref="VirtualButton.ConsumeBuffer"/>
        public void ConsumeBuffer() => Button?.ConsumeBuffer();

        /// <inheritdoc cref="VirtualButton.ConsumePress"/>
        public void ConsumePress() => Button?.ConsumePress();

        public void SetRepeat(float repeatTime) => Button?.SetRepeat(repeatTime);

        public void SetRepeat(float repeatTime, float multiRepeatTime) => Button?.SetRepeat(repeatTime, multiRepeatTime);

        #endregion

        public static implicit operator bool(ButtonBinding binding) => binding.Button ?? false;

    }

    /// <summary>
    /// Sets the default <see cref="Buttons"/> and <see cref="Keys"/> of a <see cref="ButtonBinding"/> setting.
    /// <br></br>
    /// <see href="https://github.com/EverestAPI/Resources/wiki/Mod-Settings#DefaultButtonBinding">Read More</see>
    /// </summary>
    public class DefaultButtonBindingAttribute : Attribute {

        /// <summary>
        /// The default Button binding.
        /// </summary>
        public Buttons Button;

        /// <summary>
        /// The default Key binding.
        /// </summary>
        public Keys Key;

        /// <summary>
        /// The default Buttons binding.
        /// </summary>
        public Buttons[] Buttons;

        /// <summary>
        /// The default Keys binding.
        /// </summary>
        public Keys[] Keys;

        /// <summary>
        /// Whether the default Button should always be bound.
        /// </summary>
        [Obsolete("This is no longer respected by the new input system.")]
        public bool ForceDefaultButton;

        /// <summary>
        /// Whether the default Key should always be bound.
        /// </summary>
        [Obsolete("This is no longer respected by the new input system.")]
        public bool ForceDefaultKey;

        /// <summary>
        /// Sets the default <see cref="Buttons"/> and <see cref="Keys"/> of a <see cref="ButtonBinding"/> setting.
        /// </summary>
        /// <param name="button">The default Button binding.</param>
        /// <param name="key">The default Key binding.</param>
        public DefaultButtonBindingAttribute(Buttons button, Keys key) {
            Button = button;
            Key = key;
        }

        /// <summary>
        /// Sets the default <see cref="Buttons"/> and <see cref="Keys"/> of a <see cref="ButtonBinding"/> setting.
        /// </summary>
        /// <param name="buttons">The default Buttons binding.</param>
        /// <param name="keys">The default Keys binding.</param>
        public DefaultButtonBindingAttribute(Buttons[] buttons, Keys[] keys) {
            Buttons = buttons;
            Keys = keys;
        }

    }
}
