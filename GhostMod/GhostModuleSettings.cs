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

namespace Celeste.Mod.Ghost {
    [SettingName("Ghost-eline")] // We're lazy.
    public class GhostModuleSettings : EverestModuleSettings {

        public bool Enabled { get; set; } = true;

        [SettingRange(0, 10)]
        public int Opacity { get; set; } = 5;
        [YamlIgnore]
        [SettingIgnore]
        public float OpacityFactor => Opacity / 10f;

    }
}
