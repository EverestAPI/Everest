﻿using FMOD.Studio;
using System;

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

        public override void LogRegistration() {
            Logger.Log(LogLevel.Info, "core", $"Non-code module {Metadata} registered.");
        }
        
        public override void LogUnregistration() {
            Logger.Log(LogLevel.Info, "core", $"Non-code module {Metadata} unregistered.");
        }

    }
}
