using MonoMod;
using System;

namespace Monocle {
    class patch_RendererList {

        // We're effectively in RendererList, but still need to "expose" private fields to our mod.

        [MonoModIgnore]
        [MonoModPublic]
        [MonoModLinkFrom("System.Void Monocle.RendererList::_UpdateLists()")]
        public extern void UpdateLists();

    }
    public static class RendererListExt {

        /// <summary>
        /// Update the renderer lists - apply any pending additions or removals.
        /// </summary>
        [Obsolete("Use RendererList.UpdateLists instead.")]
        public static void UpdateLists(this RendererList self)
            => ((patch_RendererList) (object) self).UpdateLists();

    }
}
