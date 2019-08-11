using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.Utils;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using NLua;

namespace Celeste.Mod {
    public class LuaModule : EverestModule {

        public override Type SettingsType => null;
        public override Type SaveDataType => null;

        public LuaModule(EverestModuleMetadata metadata) {
            Metadata = metadata;

            Everest.LuaLoader.Require(metadata.Name + ":main");
        }

        public override void Load() {
        }

        public override void Unload() {
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
        }

    }
}
