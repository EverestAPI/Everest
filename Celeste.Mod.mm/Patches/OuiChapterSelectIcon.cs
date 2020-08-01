#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Monocle;

namespace Celeste {
    class patch_OuiChapterSelectIcon : OuiChapterSelectIcon {

        // We're effectively in OuiChapterSelectIcon, but still need to "expose" private fields to our mod.
        private bool hidden;
        public bool IsHidden => hidden;

        private bool selected;
        public bool IsSelected => selected;

        public patch_OuiChapterSelectIcon(int area, MTexture front, MTexture back)
            : base(area, front, back) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

    }
    public static class OuiChapterSelectIconExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static bool GetIsHidden(this OuiChapterSelectIcon self)
            => ((patch_OuiChapterSelectIcon) self).IsHidden;

        public static bool GetIsSelected(this OuiChapterSelectIcon self)
            => ((patch_OuiChapterSelectIcon) self).IsSelected;

    }
}
