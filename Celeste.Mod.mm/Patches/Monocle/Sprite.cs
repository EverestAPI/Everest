#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;

namespace Monocle {
    // The only patch_ that needs to be made public to allow accessing .Animation
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

        public static Dictionary<string, patch_Sprite.Animation> GetAnimations(this Sprite self)
            => ((patch_Sprite) self).Animations;

    }
}
