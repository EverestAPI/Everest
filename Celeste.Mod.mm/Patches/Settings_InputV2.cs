using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Celeste {
    [MonoModIfFlag("V2:Input")]
    [MonoModPatch("Settings")]
    class patch_Settings_InputV2 : Settings {

#pragma warning disable CS0649 // static field staying null
        [MonoModIgnore]
        public static new patch_Settings_InputV2 Instance;
#pragma warning restore CS0649

        private static List<Keys> _EnumToList(Keys all)
            => Enum.GetValues(typeof(Keys)).Cast<Keys>().Where(v => (all & v) == v).ToList();

        private static Keys _ListToEnum(List<Keys> list) {
            Keys all = default;
            foreach (Keys v in list)
                all |= v;
            return all;
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Left")]
        [XmlIgnore]
        public Keys Left_V1 {
            get => _ListToEnum(Left.Keyboard);
            set => Left.Keyboard = _EnumToList(value);
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Right")]
        [XmlIgnore]
        public Keys Right_V1 {
            get => _ListToEnum(Right.Keyboard);
            set => Right.Keyboard = _EnumToList(value);
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Down")]
        [XmlIgnore]
        public Keys Down_V1 {
            get => _ListToEnum(Down.Keyboard);
            set => Down.Keyboard = _EnumToList(value);
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Up")]
        [XmlIgnore]
        public Keys Up_V1 {
            get => _ListToEnum(Up.Keyboard);
            set => Up.Keyboard = _EnumToList(value);
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Grab")]
        [XmlIgnore]
        public List<Keys> Grab_V1 {
            get => Grab.Keyboard;
            set => Grab.Keyboard = value;
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Jump")]
        [XmlIgnore]
        public List<Keys> Jump_V1 {
            get => Jump.Keyboard;
            set => Jump.Keyboard = value;
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Dash")]
        [XmlIgnore]
        public List<Keys> Dash_V1 {
            get => Dash.Keyboard;
            set => Dash.Keyboard = value;
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Talk")]
        [XmlIgnore]
        public List<Keys> Talk_V1 {
            get => Talk.Keyboard;
            set => Talk.Keyboard = value;
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Pause")]
        [XmlIgnore]
        public List<Keys> Pause_V1 {
            get => Pause.Keyboard;
            set => Pause.Keyboard = value;
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Confirm")]
        [XmlIgnore]
        public List<Keys> Confirm_V1 {
            get => Confirm.Keyboard;
            set => Confirm.Keyboard = value;
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Cancel")]
        [XmlIgnore]
        public List<Keys> Cancel_V1 {
            get => Cancel.Keyboard;
            set => Cancel.Keyboard = value;
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Journal")]
        [XmlIgnore]
        public List<Keys> Journal_V1 {
            get => Journal.Keyboard;
            set => Journal.Keyboard = value;
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::QuickRestart")]
        [XmlIgnore]
        public List<Keys> QuickRestart_V1 {
            get => QuickRestart.Keyboard;
            set => QuickRestart.Keyboard = value;
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::DemoDash")]
        [XmlIgnore]
        public List<Keys> DemoDash_V1 {
            get => DemoDash.Keyboard;
            set => DemoDash.Keyboard = value;
        }

        [XmlIgnore]
        public List<Buttons> BtnGrab {
            get => Grab.Controller;
            set => Grab.Controller = value;
        }

        [XmlIgnore]
        public List<Buttons> BtnJump {
            get => Jump.Controller;
            set => Jump.Controller = value;
        }

        [XmlIgnore]
        public List<Buttons> BtnDash {
            get => Dash.Controller;
            set => Dash.Controller = value;
        }

        [XmlIgnore]
        public List<Buttons> BtnTalk {
            get => Talk.Controller;
            set => Talk.Controller = value;
        }

        [XmlIgnore]
        public List<Buttons> BtnAltQuickRestart {
            get => QuickRestart.Controller;
            set => QuickRestart.Controller = value;
        }

        [XmlIgnore]
        public List<Buttons> BtnDemoDash {
            get => DemoDash.Controller;
            set => DemoDash.Controller = value;
        }

    }
}
