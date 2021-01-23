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

        [MonoModIfFlag("V1:Input")]
        public List<Buttons> Buttons {
            [MonoModIfFlag("V1:Input")]
            get;
            [MonoModIfFlag("V1:Input")]
            set;
        }
        [MonoModIfFlag("V1:Input")]
        public List<Keys> Keys {
            [MonoModIfFlag("V1:Input")]
            get;
            [MonoModIfFlag("V1:Input")]
            set;
        }

        [MonoModIfFlag("V2:Input")]
        [MonoModPatch("Buttons")]
        public List<Buttons> Buttons_V1 {
            [MonoModIfFlag("V2:Input")]
            [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Buttons> Celeste.Mod.ButtonBinding::get_Buttons()")]
            get => Binding.Controller;
            [MonoModIfFlag("V2:Input")]
            [MonoModLinkFrom("System.Void Celeste.Mod.ButtonBinding::set_Buttons(System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Buttons>)")]
            set => Binding.Controller = value;
        }
        [MonoModIfFlag("V2:Input")]
        [MonoModPatch("Keys")]
        public List<Keys> Keys_V1 {
            [MonoModIfFlag("V2:Input")]
            [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Mod.ButtonBinding::get_Keys()")]
            get => Binding.Keyboard;
            [MonoModIfFlag("V2:Input")]
            [MonoModLinkFrom("System.Void Celeste.Mod.ButtonBinding::set_Keys(System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys>)")]
            set => Binding.Keyboard = value;
        }

        [MonoModIfFlag("V2:Input")]
        private Binding _Binding;

        [MonoModIfFlag("V2:Input")]
        [YamlIgnore] // Binding uses XmlIgnores and migrating old mappings is hard.
        public Binding Binding {
            [MonoModIfFlag("V2:Input")]
            get => _Binding;
            [MonoModIfFlag("V2:Input")]
            private set => _Binding = Binding;
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
        [Obsolete("Please move to Binding ASAP (ideally after the new Celeste input system gets out of beta).")]
        public List<patch_VirtualButton_InputV1.Node> Nodes {
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
        public patch_VirtualButton_InputV1 Button;

        public ButtonBinding()
            : this(0) {
        }

        public ButtonBinding(Buttons buttons, params Keys[] keys) {
            Init(buttons, keys);
        }

        [MonoModIgnore]
        private extern void Init(Buttons buttons, params Keys[] keys);

        [MonoModIfFlag("V1:Input")]
        [MonoModPatch("Init")]
        [MonoModReplace]
        [Obsolete]
        private void InitV1(Buttons buttons, params Keys[] keys) {
            Buttons = Enum.GetValues(typeof(Buttons)).Cast<Buttons>().Where(b => (buttons & b) == b).ToList();
            Keys = new List<Keys>(keys);
        }

        [MonoModIfFlag("V2:Input")]
        [MonoModPatch("Init")]
        [MonoModReplace]
        private void InitV2(Buttons buttons, params Keys[] keys) {
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
        /// Whether the default Button should always be bound.
        /// </summary>
        // FIXME!!! Currently unused in V2 menu!
        public bool ForceDefaultButton;

        /// <summary>
        /// Whether the default Key should always be bound.
        /// </summary>
        // FIXME!!! Currently unused in V2 menu!
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
