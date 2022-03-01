using MonoMod;

namespace Celeste {
    // Extend patch_Item so we can override AlwaysRender properly
    class patch_InputMappingInfo : patch_TextMenu.patch_Item {
        // we want it to always render, as it overlays on top of the menu when scrolling down (so it never goes off-screen).
        public override bool AlwaysRender => true;
    }
}
