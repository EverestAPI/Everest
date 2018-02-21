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

namespace Celeste.Mod.Rainbow {
    public class RainbowModuleSettings : EverestModuleSettings {

        public bool Enabled { get; set; } = false;

        [SettingRange(0, 20)]
        public int Speed { get; set; } = 10;
        [YamlIgnore]
        [SettingIgnore]
        public float SpeedFactor => Speed / 20f;

    }
}
