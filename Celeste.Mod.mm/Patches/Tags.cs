#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Monocle;

namespace Celeste {
    // Tags is static.
    class patch_Tags {

        public static extern void orig_Initialize();
        public static void Initialize() {
            orig_Initialize();
            TagsExt.SubHUD = new BitTag("subHUD");
        }

    }
    public static class TagsExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Tag to be used for entities rendering like a HUD, but below the actual game HUD.
        /// </summary>
        public static BitTag SubHUD;

    }
}
