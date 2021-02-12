#pragma warning disable CS0649 // field staying default

using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Celeste {
    [MonoModIfFlag("V1:Input")]
    [MonoModPatch("Settings")]
    class patch_Settings_InputV1 : Settings {

        [MonoModIgnore]
        public static new patch_Settings_InputV1 Instance;

        // Only present between approx. 1.3.3.10 and 1.3.3.12
        [MonoModIgnore]
        public bool RevealDemoConfig;

        [MonoModIgnore]
        public new Keys Left;

        [MonoModIgnore]
        public new Keys Right;

        [MonoModIgnore]
        public new Keys Down;

        [MonoModIgnore]
        public new Keys Up;

        [MonoModIgnore]
        public new List<Keys> Grab;

        [MonoModIgnore]
        public new List<Keys> Jump;

        [MonoModIgnore]
        public new List<Keys> Dash;

        [MonoModIgnore]
        public new List<Keys> Talk;

        [MonoModIgnore]
        public new List<Keys> Pause;

        [MonoModIgnore]
        public new List<Keys> Confirm;

        [MonoModIgnore]
        public new List<Keys> Cancel;

        [MonoModIgnore]
        public new List<Keys> Journal;

        [MonoModIgnore]
        public new List<Keys> QuickRestart;

        [MonoModIgnore]
        public new List<Keys> DemoDash;

        [MonoModIgnore]
        public List<Buttons> BtnGrab;

        [MonoModIgnore]
        public List<Buttons> BtnJump;

        [MonoModIgnore]
        public List<Buttons> BtnDash;

        [MonoModIgnore]
        public List<Buttons> BtnTalk;

        [MonoModIgnore]
        public List<Buttons> BtnAltQuickRestart;

        [MonoModIgnore]
        public List<Buttons> BtnDemoDash;

        // Technically unrelated from the Input V1 / V2 split but these changes were introduced at the same time...

        [MonoModIgnore]
        public bool DisableScreenShake;

        public ScreenshakeAmount SceenShake {
            get => DisableScreenShake ? ScreenshakeAmount.Off : ScreenshakeAmount.On;
            set => DisableScreenShake = value == ScreenshakeAmount.Off;
        }

    }

    [MonoModIfFlag("V1:Input")]
    [MonoModPatch("ScreenshakeAmount")]
    enum ScreenshakeAmount_InputV1 {
        [XmlEnum("false")]
        Off,
        [XmlEnum("true")]
        Half,
        On
    }
}
