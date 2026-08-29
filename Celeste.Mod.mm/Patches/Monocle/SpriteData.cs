#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;
using System.Collections.Generic;

namespace Monocle {
    class patch_SpriteData : SpriteData {

        public patch_SpriteData(Atlas atlas)
            : base(atlas) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModReplace]
        private bool HasFrames(patch_Atlas atlas, string path, int[] frames = null) {
            if (frames == null || frames.Length == 0) {
                return atlas.GetAtlasSubtexturesAt(path, 0, null) != null;
            }
            for (int i = 0; i < frames.Length; i++) {
                if (atlas.GetAtlasSubtexturesAt(path, frames[i], null) == null) {
                    return false;
                }
            }
            return true;
        }
    }
}
