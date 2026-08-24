#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Monocle;

namespace Celeste {
    // Tags is static.
    class patch_Tags {

        public static extern void orig_Initialize();
        public static void Initialize() {
            orig_Initialize();
            TagsExt.SubHUD = new BitTag("subHUD");
            TagsExt.FreezeFrameUpdate = new BitTag("freezeFrameUpdate");
        }

    }
    public static class TagsExt {

        /// <summary>
        /// Tag to be used for entities rendering like a HUD, but below the actual game HUD.
        /// </summary>
        public static BitTag SubHUD;

        /// <summary>
        /// Tag to be used for entities that should update during freeze frames.<br/>
        /// If in a <see cref="Level"/>, <see cref="Tags.PauseUpdate"/> is also required for this entity to update during freeze frames when the level is <c>Paused</c>,
        /// <see cref="Tags.TransitionUpdate"/> is required to update during a transition, and <see cref="Tags.FrozenUpdate"/> is required to update when the level is <c>Frozen</c>.
        /// </summary>
        public static BitTag FreezeFrameUpdate;
        
    }
}
