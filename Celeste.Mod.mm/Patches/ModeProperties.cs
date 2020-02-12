using Celeste.Mod.Meta;

namespace Celeste {
    public class patch_ModeProperties : ModeProperties {
        // Store the metadata in the corresponding mode
        public MapMeta MapMeta;
    }

    public static class ModePropertiesExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static MapMeta GetMapMeta(this ModeProperties self)
            => ((patch_ModeProperties) self).MapMeta;
        public static void SetMapMeta(this ModeProperties self, MapMeta value)
            => ((patch_ModeProperties) self).MapMeta = value;
    }
}