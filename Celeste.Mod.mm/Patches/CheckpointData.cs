namespace Celeste {
    class patch_CheckpointData : CheckpointData {

        public AreaKey Area;

        public patch_CheckpointData(string level, string name, PlayerInventory? inventory = null, bool dreaming = false, AudioState audioState = null)
            : base(level, name, inventory, dreaming, audioState) {
        }

    }
    public static class CheckpointDataExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static AreaKey GetArea(this CheckpointData self)
            => ((patch_CheckpointData) self).Area;
        public static void SetArea(this CheckpointData self, AreaKey value)
            => ((patch_CheckpointData) self).Area = value;

    }
}
