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
    // Note: If SettingsName isn't given, the value defaults to modoptions_[typename without settings]_title
    [SettingName("modoptions_coremodule_title")]
    public class CoreModuleSettings : EverestModuleSettings {

        // Note: If SettingsName isn't given, the values default to modoptions_[typename without settings]_[fieldname]

        //[SettingName("modoptions_coremodule_rainbowmode")]
        public bool RainbowMode { get; set; } = false;

        [SettingRange(0, 10)]
        [SettingInGame(true)]
        public int ExampleSlider { get; set; } = 5;

    }
}
