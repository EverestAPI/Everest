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
        [SettingName("Near Opacity")]
        public int InnerOpacity { get; set; } = 5;
        [YamlIgnore]
        [SettingIgnore]
        public float InnerOpacityFactor => InnerOpacity / 10f;

        [SettingRange(0, 10)]
        [SettingName("Far Opacity")]
        public int OuterOpacity { get; set; } = 1;
        [YamlIgnore]
        [SettingIgnore]
        public float OuterOpacityFactor => OuterOpacity / 10f;

        [SettingRange(1, 10)]
        [SettingName("Near Radius")]
        public int InnerRadius { get; set; } = 3;
        [YamlIgnore]
        [SettingIgnore]
        public float InnerRadiusDist => InnerRadius * InnerRadius * 64f;

        [SettingRange(1, 10)]
        [SettingName("Gradient Region")]
        public int BorderSize { get; set; } = 4;
        [YamlIgnore]
        [SettingIgnore]
        public float BorderSizeDist => BorderSize * BorderSize * 64f;

    }
}
