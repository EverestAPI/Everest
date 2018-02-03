using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod {
    // Note: If SettingName isn't given, the value defaults to modoptions_[typename without settings]_title
    [SettingName("modoptions_coremodule_title")]
    public class CoreModuleSettings : EverestModuleSettings {

        // Note: If SettingName isn't given, the values default to modoptions_[typename without settings]_[fieldname]

        // Example runtime setting that only shows up in the menu, not the settings file.
        // [SettingName("modoptions_coremodule_debugmode")]
        [YamlIgnore]
        [SettingNeedsRelaunch]
        public bool LaunchInDebugMode {
            get {
                return Settings.Instance.LaunchInDebugMode;
            }
            set {
                Settings.Instance.LaunchInDebugMode = value;
            }
        }

        [SettingNeedsRelaunch]
        public bool LaunchWithoutIntro { get; set; } = false;

        /*
        [SettingRange(0, 10)]
        public int ExampleSlider { get; set; } = 5;

        [SettingRange(0, 10)]
        [SettingInGame(false)]
        public int ExampleMainMenuSlider { get; set; } = 5;

        [SettingRange(0, 10)]
        [SettingInGame(true)]
        public int ExampleInGameSlider { get; set; } = 5;
        */

    }
}
