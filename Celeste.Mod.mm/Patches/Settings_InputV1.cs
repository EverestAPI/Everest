using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste {
    [MonoModIgnore]
    class patch_Settings_InputV1 : Settings {

#pragma warning disable CS0649 // field staying default

        [MonoModIgnore]
        public static new patch_Settings_InputV1 Instance;

        // Only present between approx. 1.3.3.10 and 1.3.3.12
        public bool RevealDemoConfig;

        public new Keys Left;

        public new Keys Right;

        public new Keys Down;

        public new Keys Up;

        public new List<Keys> Grab;

        public new List<Keys> Jump;

        public new List<Keys> Dash;

        public new List<Keys> Talk;

        public new List<Keys> Pause;

        public new List<Keys> Confirm;

        public new List<Keys> Cancel;

        public new List<Keys> Journal;

        public new List<Keys> QuickRestart;

        public new List<Keys> DemoDash;

        public List<Buttons> BtnGrab;

        public List<Buttons> BtnJump;

        public List<Buttons> BtnDash;

        public List<Buttons> BtnTalk;

        public List<Buttons> BtnAltQuickRestart;

        public List<Buttons> BtnDemoDash;

    }
}
