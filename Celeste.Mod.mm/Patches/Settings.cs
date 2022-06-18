using Microsoft.Xna.Framework.Input;
using MonoMod;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Celeste {
    class patch_Settings : Settings {

        [MonoModIgnore]
        [PatchSettingsDoNotTranslateKeys]
        public extern new void SetDefaultKeyboardControls(bool reset);

        #region Legacy Input

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Left")]
        [XmlIgnore]
        public Keys Left_V1 {
            get => Left.Keyboard.FirstOrDefault();
            set => Left.Keyboard = new List<Keys> { value };
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Right")]
        [XmlIgnore]
        public Keys Right_V1 {
            get => Right.Keyboard.FirstOrDefault();
            set => Right.Keyboard = new List<Keys> { value };
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Down")]
        [XmlIgnore]
        public Keys Down_V1 {
            get => Down.Keyboard.FirstOrDefault();
            set => Down.Keyboard = new List<Keys> { value };
        }

        [MonoModLinkFrom("Microsoft.Xna.Framework.Input.Keys Celeste.Settings::Up")]
        [XmlIgnore]
        public Keys Up_V1 {
            get => Up.Keyboard.FirstOrDefault();
            set => Up.Keyboard = new List<Keys> { value };
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Grab")]
        [XmlIgnore]
        public List<Keys> Grab_V1 {
            get => Grab.Keyboard;
            set => Grab.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Jump")]
        [XmlIgnore]
        public List<Keys> Jump_V1 {
            get => Jump.Keyboard;
            set => Jump.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Dash")]
        [XmlIgnore]
        public List<Keys> Dash_V1 {
            get => Dash.Keyboard;
            set => Dash.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Talk")]
        [XmlIgnore]
        public List<Keys> Talk_V1 {
            get => Talk.Keyboard;
            set => Talk.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Pause")]
        [XmlIgnore]
        public List<Keys> Pause_V1 {
            get => Pause.Keyboard;
            set => Pause.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Confirm")]
        [XmlIgnore]
        public List<Keys> Confirm_V1 {
            get => Confirm.Keyboard;
            set => Confirm.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Cancel")]
        [XmlIgnore]
        public List<Keys> Cancel_V1 {
            get => Cancel.Keyboard;
            set => Cancel.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::Journal")]
        [XmlIgnore]
        public List<Keys> Journal_V1 {
            get => Journal.Keyboard;
            set => Journal.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Keys> Celeste.Settings::QuickRestart")]
        [XmlIgnore]
        public List<Keys> QuickRestart_V1 {
            get => QuickRestart.Keyboard;
            set => QuickRestart.Keyboard = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Buttons> Celeste.Settings::BtnGrab")]
        [XmlIgnore]
        public List<Buttons> BtnGrab {
            get => Grab.Controller;
            set => Grab.Controller = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Buttons> Celeste.Settings::BtnJump")]
        [XmlIgnore]
        public List<Buttons> BtnJump {
            get => Jump.Controller;
            set => Jump.Controller = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Buttons> Celeste.Settings::BtnDash")]
        [XmlIgnore]
        public List<Buttons> BtnDash {
            get => Dash.Controller;
            set => Dash.Controller = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Buttons> Celeste.Settings::BtnTalk")]
        [XmlIgnore]
        public List<Buttons> BtnTalk {
            get => Talk.Controller;
            set => Talk.Controller = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Buttons> Celeste.Settings::BtnAltQuickRestart")]
        [XmlIgnore]
        public List<Buttons> BtnAltQuickRestart {
            get => QuickRestart.Controller;
            set => QuickRestart.Controller = value;
        }

        [MonoModLinkFrom("System.Collections.Generic.List`1<Microsoft.Xna.Framework.Input.Buttons> Celeste.Settings::BtnDemoDash")]
        [XmlIgnore]
        public List<Buttons> BtnDemoDash {
            get => DemoDash.Controller;
            set => DemoDash.Controller = value;
        }

        // Technically unrelated from the Input V1 / V2 split but these changes were introduced at the same time...

        [XmlIgnore]
        [MonoModLinkFrom("System.Boolean Celeste.Settings::DisableScreenShake")]
        public bool DisableScreenShake {
            get => ScreenShake == ScreenshakeAmount.Off;
            set => ScreenShake = value ? ScreenshakeAmount.Off : ScreenshakeAmount.On;
        }

        #endregion

    }
}
