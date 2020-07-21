using Microsoft.Xna.Framework.Input;
using Monocle;
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
        public List<Buttons> Buttons { get; set; }
        public List<Keys> Keys { get; set; }

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
        public List<VirtualButton.Node> Nodes {
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
        public VirtualButton Button;

        public ButtonBinding()
            : this(0) {
        }

        public ButtonBinding(Buttons buttons, params Keys[] keys) {
            Buttons = Enum.GetValues(typeof(Buttons)).Cast<Buttons>().Where(b => (buttons & b) == b).ToList();
            Keys = new List<Keys>(keys);
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
        /// Whether the default Button should always be bound.
        /// </summary>
        public bool ForceDefaultButton;

        /// <summary>
        /// Whether the default Key should always be bound.
        /// </summary>
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

    }
}
