using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Celeste {
    class patch_Settings : Settings {

        [MonoModIgnore]
        public new bool SpeedrunClock;
        [MonoModIfFlag("Fill:SpeedrunType")]
        public patch_SpeedrunType SpeedrunClock_Typed {
            get {
                return SpeedrunClock ? patch_SpeedrunType.Chapter : patch_SpeedrunType.Off;
            }
            set {
                SpeedrunClock = value != patch_SpeedrunType.Off;
            }
        }

        [MonoModIfFlag("Fill:LaunchInDebugMode")]
        public new bool LaunchInDebugMode {
            get {
                return false;
            }
            set {
            }
        }

        [MonoModIfFlag("Fill:LaunchWithFMODLiveUpdate")]
        public new bool LaunchWithFMODLiveUpdate {
            get {
                return false;
            }
            set {
            }
        }

    }

    [MonoModIfFlag("Fill:SpeedrunType")]
    enum patch_SpeedrunType {
        [XmlEnum("false")]
        Off,
        [XmlEnum("true")]
        Chapter,
        File
    }
}
