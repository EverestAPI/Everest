using System;

namespace Celeste {
    class patch_CheckpointData : CheckpointData {

        public AreaKey Area;

        public patch_CheckpointData(string level, string name, PlayerInventory? inventory = null, bool dreaming = false, AudioState audioState = null)
            : base(level, name, inventory, dreaming, audioState) {
        }

    }
    public static class CheckpointDataExt {

        [Obsolete("Use CheckpointData.Area instead.")]
        public static AreaKey GetArea(this CheckpointData self)
            => ((patch_CheckpointData) self).Area;
        [Obsolete("Use CheckpointData.Area instead.")]
        public static void SetArea(this CheckpointData self, AreaKey value)
            => ((patch_CheckpointData) self).Area = value;

    }
}
