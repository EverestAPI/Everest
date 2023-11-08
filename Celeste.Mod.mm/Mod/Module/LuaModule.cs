using FMOD.Studio;
using System;

namespace Celeste.Mod {
    public class LuaModule : EverestModule {

        public override Type SettingsType => null;
        public override Type SaveDataType => null;
        public override Type SessionType => null;

        public LuaModule(EverestModuleMetadata metadata) {
            Metadata = metadata;

            Everest.LuaLoader.Require($"{metadata.Name}:/main");
        }

        public override void Load() {
        }

        public override void Unload() {
        }

        public override void CreateModMenuSection(patch_TextMenu menu, bool inGame, EventInstance snapshot) {
        }
        
        public override void LogRegistration() {
            Logger.Log(LogLevel.Info, "core", $"Lua module {Metadata} registered.");
        }
        
        public override void LogUnregistration() {
            Logger.Log(LogLevel.Info, "core", $"Lua module {Metadata} unregistered.");
        }

    }
}
