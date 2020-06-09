using Celeste.Mod.UI;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod {
    public class ButtonBinding {

        public List<Buttons> Buttons { get; set; }
        public List<Keys> Keys { get; set; }

        [YamlIgnore]
        public VirtualButton Button;

        public ButtonBinding()
            : this(0) {
        }

        public ButtonBinding(Buttons buttons, params Keys[] keys) {
            Buttons = Enum.GetValues(typeof(Buttons)).Cast<Buttons>().Where(b => (buttons & b) == b).ToList();
            Keys = new List<Keys>(keys);
        }

    }

    public class DefaultButtonBindingAttribute : Attribute {

        public Buttons Button;
        public Keys Key;
        public bool ForceDefaultButton;
        public bool ForceDefaultKey;

        public DefaultButtonBindingAttribute(Buttons button, Keys key) {
            Button = button;
            Key = key;
        }

    }
}
