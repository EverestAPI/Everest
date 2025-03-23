using Celeste;
using Mono.Cecil;
using MonoMod;
using System;

namespace Celeste {
    public class patch_PlayerSprite : PlayerSprite {

        public patch_PlayerSprite(PlayerSpriteMode mode) : base(mode) {
            // Do nothing here. MonoMod will ignore this.
        }

        [MonoModIgnore]
        [ForceNoInlining]
        public new extern static void ClearFramesMetadata();
    }
}
