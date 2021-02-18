#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;
using System.Collections.Generic;

namespace Monocle {
    class patch_SpriteData : SpriteData {

        public patch_SpriteData(Atlas atlas)
            : base(atlas) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        private extern bool orig_HasFrames(patch_Atlas atlas, string path, int[] frames = null);
        private bool HasFrames(patch_Atlas atlas, string path, int[] frames = null) {
            atlas.PushFallback(null);
            bool rv = orig_HasFrames(atlas, path, frames);
            atlas.PopFallback();
            return rv;
        }

    }
}
