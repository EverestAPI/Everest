#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Monocle;

namespace Celeste {
    // Tags is static.
    class patch_Tags {

        public static extern void orig_Initialize();
        public static void Initialize() {
            orig_Initialize();
            TagsExt.SubHUD = new BitTag("subHUD");
            TagsExt.FreezeUpdate = new BitTag("freezeUpdate");
        }

    }
    public static class TagsExt {

        /// <summary>
        /// Tag to be used for entities rendering like a HUD, but below the actual game HUD.
        /// </summary>
        public static BitTag SubHUD;

        /// <summary>
        /// Tag to be used for entities that should update during freeze frames.
        /// </summary>
        public static BitTag FreezeUpdate;
    }
}
