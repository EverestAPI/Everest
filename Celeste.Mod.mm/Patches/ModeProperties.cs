using Celeste.Mod.Meta;
using System;

namespace Celeste {
    public class patch_ModeProperties : ModeProperties {
        // Store the metadata in the corresponding mode
        public MapMeta MapMeta;
        public new patch_MapData MapData;
    }

    public static class ModePropertiesExt {

        [Obsolete("Use ModeProperties.MapMeta instead.")]
        public static MapMeta GetMapMeta(this ModeProperties self)
            => ((patch_ModeProperties) self).MapMeta;
        [Obsolete("Use ModeProperties.MapMeta instead.")]
        public static void SetMapMeta(this ModeProperties self, MapMeta value)
            => ((patch_ModeProperties) self).MapMeta = value;
    }
}