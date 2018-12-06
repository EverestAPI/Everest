using Celeste.Editor;
using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using Celeste;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using Celeste.Mod.Helpers;
using MonoMod.Utils;
using Microsoft.Xna.Framework.Input;
using System.Threading;

namespace Celeste.Mod {
    internal class NullModule : EverestModule {

        public override Type SettingsType => null;
        public override Type SaveDataType => null;

        public NullModule(EverestModuleMetadata metadata) {
            Metadata = metadata;
        }

        public override void Load() {
        }

        public override void Unload() {
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
        }

    }
}
