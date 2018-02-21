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

namespace Celeste.Mod.Energy {
    public class MadelineEnergySelectorSettings : EverestModuleSettings {

        public bool Enabled { get; set; } = false;

        [SettingRange(0, 15)]
        public int Excitement { get; set; } = 1;

    }
}
