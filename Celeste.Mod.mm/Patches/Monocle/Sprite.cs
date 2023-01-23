#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod.Helpers;
using MonoMod;
using System.Collections.Generic;

namespace Monocle {
    // The only patch_ that needs to be made public to allow accessing .Animation
    [Tracked]
    public class patch_Sprite : Sprite {

        private Dictionary<string, Animation> animations;
        public Dictionary<string, Animation> Animations => animations;

        public patch_Sprite(Atlas atlas, string path)
            : base(atlas, path) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModPublic]
        [MonoModIgnore]
        public class Animation {
            public float Delay;
            public MTexture[] Frames;
            public Chooser<string> Goto;
        }

        internal class FallbackSprite : patch_Sprite {
            public FallbackSprite(Atlas atlas)
                : base(atlas, "__fallback") {
                this.animations = new Dictionary<string, Animation>(new AlwaysEqual<string>());
                this.Add("__fallback", "__fallback");
            }
        }

    }
    public static class SpriteExt {

        public static Dictionary<string, patch_Sprite.Animation> GetAnimations(this Sprite self)
            => ((patch_Sprite) self).Animations;

    }
}
