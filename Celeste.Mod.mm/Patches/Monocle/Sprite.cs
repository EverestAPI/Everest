#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using MonoMod;
using System;
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

    }
    public static class SpriteExt {

        [Obsolete("Use Sprite.Animations instead.")]
        public static Dictionary<string, patch_Sprite.Animation> GetAnimations(this Sprite self)
            => ((patch_Sprite) self).Animations;

    }
}
