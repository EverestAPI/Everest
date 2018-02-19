using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod.Detour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Ghost {
    public class GhostModule : EverestModule {

        public static GhostModule Instance;

        public override Type SettingsType => typeof(GhostModuleSettings);
        public static GhostModuleSettings Settings => (GhostModuleSettings) Instance._Settings;
        public override Type SaveDataType  => typeof(GhostModuleSaveData);
        public static GhostModuleSaveData SaveData => (GhostModuleSaveData) Instance._SaveData;

        public GhostModule() {
            Instance = this;
        }

        public override void Load() {
        }

        public override void Unload() {
        }

        

    }
}
