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

        // Introduced in 1.3.3.19, can be safely ignored in older versions..?
        public new CrouchDashModesShim CrouchDashMode;

        // Technically unrelated from the Input V1 / V2 split but these changes were introduced at the same time...

        [MonoModIgnore]
        public bool DisableScreenShake;

        [XmlIgnore]
        public new ScreenshakeAmountShim ScreenShake {
            [MonoModLinkFrom("Celeste.ScreenshakeAmount Celeste.Settings::get_ScreenShake()")]
            get => DisableScreenShake ? ScreenshakeAmountShim.Off : ScreenshakeAmountShim.On;
            [MonoModLinkFrom("System.Void Celeste.Settings::set_ScreenShake(Celeste.ScreenshakeAmount)")]
            set => DisableScreenShake = value == ScreenshakeAmountShim.Off;
        }

    }

    [MonoModIfFlag("V1:Input")]
    [ForceName("ScreenshakeAmount")]
    public enum ScreenshakeAmountShim {
        [XmlEnum("false")]
        Off,
        [XmlEnum("true")]
        Half,
        On
    }

    [MonoModIfFlag("V1:Input")]
    [ForceName("CrouchDashModes")]
    public enum CrouchDashModesShim {
        Press,
        Hold
    }
}
