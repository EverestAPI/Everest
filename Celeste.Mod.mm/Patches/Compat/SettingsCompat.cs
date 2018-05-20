using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Celeste {
    [MonoModPatch("Settings")]
    class SettingsCompat {

        [MonoModIgnore]
        public bool SpeedrunClock;
        [MonoModIfFlag("Fill:EnumSpeedrunType")]
        public patch_SpeedrunType SpeedrunClockProxy {
            get {
                return SpeedrunClock ? patch_SpeedrunType.Chapter : patch_SpeedrunType.Off;
            }
            set {
                SpeedrunClock = value != patch_SpeedrunType.Off;
            }
        }

        [MonoModIfFlag("Fill:LaunchInDebugMode")]
        public bool LaunchInDebugMode {
            get {
                return false;
            }
            set {
            }
        }

        [MonoModIfFlag("Fill:LaunchWithFMODLiveUpdate")]
        public bool LaunchWithFMODLiveUpdate {
            get {
                return false;
            }
            set {
            }
        }

    }

    [MonoModIfFlag("Fill:EnumSpeedrunType")]
    enum patch_SpeedrunType {
        [XmlEnum("false")]
        Off,
        [XmlEnum("true")]
        Chapter,
        File
    }
}
