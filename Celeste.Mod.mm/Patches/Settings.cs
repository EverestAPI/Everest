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
        [MonoModHook("Celeste.SpeedrunType Celeste.Settings::SpeedrunClock")]
        [MonoModHook("Celeste.fill_SpeedrunType Celeste.Settings::SpeedrunClock")]
        [XmlIgnore]
        public fill_SpeedrunType fill_SpeedrunClock {
            [MonoModIfFlag("Fill:SpeedrunType")]
            get {
                return SpeedrunClock ? fill_SpeedrunType.Chapter : fill_SpeedrunType.Off;
            }
            [MonoModIfFlag("Fill:SpeedrunType")]
            set {
                SpeedrunClock = value != fill_SpeedrunType.Off;
            }
        }

        [MonoModIfFlag("Fill:LaunchInDebugMode")]
        public new bool LaunchInDebugMode {
            [MonoModIfFlag("Fill:LaunchInDebugMode")]
            get {
                return false;
            }
            [MonoModIfFlag("Fill:LaunchInDebugMode")]
            set {
            }
        }

        [MonoModIfFlag("Fill:LaunchWithFMODLiveUpdate")]
        public new bool LaunchWithFMODLiveUpdate {
            [MonoModIfFlag("Fill:LaunchWithFMODLiveUpdate")]
            get {
                return false;
            }
            [MonoModIfFlag("Fill:LaunchWithFMODLiveUpdate")]
            set {
            }
        }

    }

    [MonoModIfFlag("Fill:SpeedrunType")]
    [MonoModHook("Celeste.SpeedrunType")]
    enum fill_SpeedrunType {
        [XmlEnum("false")]
        Off,
        [XmlEnum("true")]
        Chapter,
        File
    }
}
