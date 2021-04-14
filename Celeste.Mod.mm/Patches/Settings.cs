using MonoMod;

namespace Celeste {
    class patch_Settings : Settings {
        [MonoModIgnore]
        [PatchSettingsDoNotTranslateKeys]
        public extern new void SetDefaultKeyboardControls(bool reset);
    }
}
