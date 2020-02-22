#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;
using System.IO;
using Monocle;

namespace Celeste {
    class patch_Decal : Decal {

        private Vector2 scale;
        public Vector2 Scale {
            get => scale;
            set => scale = value;
        }

        public patch_Decal(string texture, Vector2 position, Vector2 scale, int depth)
            : base(texture, position, scale, depth) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        private class CoreSwapImage : Component {
            public CoreSwapImage(MTexture hot, MTexture cold) : base(active: false, visible: true) {
                // no-op
            }
        }

        [MonoModIgnore]
        private class DecalImage : Component {
            public DecalImage() : base(active: false, visible: true) {
                // no-op
            }
        }

        public extern void orig_ctor(string texture, Vector2 position, Vector2 scale, int depth);
        [MonoModConstructor]
        public void ctor(string texture, Vector2 position, Vector2 scale, int depth) {
            if (string.IsNullOrEmpty(Path.GetExtension(texture))) {
                // Cruor temporarily broke decal paths in Maple / Ahorn.
                texture += ".png";
            }

            orig_ctor(texture, position, scale, depth);
        }

        [MonoModIgnore]
        private extern void MakeParallax(float amount);
        [MonoModIgnore]
        private extern void CreateSmoke(Vector2 offset, bool inbg);
        [MonoModIgnore]
        private extern void MakeMirror(string path, bool keepOffsetsClose);
        [MonoModIgnore]
        private extern void MakeFloaty();
        [MonoModIgnore]
        private extern void MakeBanner(float speed, float amplitude, int sliceSize, float sliceSinIncrement, bool easeDown, float offset = 0f, bool onlyIfWindy = false);
        [MonoModIgnore]
        private Component image;

        public extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            orig_Added(scene);
            // Handle the Decal Registry
            string text = Name.ToLower();
            if (text.StartsWith("decals/")) {
                text = text.Substring(7);
            }
            if (DecalRegistry.RegisteredDecals.ContainsKey(text)) {
                Remove(image);
                image = null;
                DecalRegistry.DecalInfo info = DecalRegistry.RegisteredDecals[text];
                if (info.CoreSwap) {
                    Add(image = new CoreSwapImage(GFX.Game[$"decals/{info.CoreSwapHotPath}"], GFX.Game[$"decals/{info.CoreSwapColdPath}"]));
                }
                if (info.AnimationSpeed != -1f) {
                    AnimationSpeed = info.AnimationSpeed;
                }
                if (info.Depth != -1)
                    Depth = info.Depth;
                if (info.ParallaxAmt != 0f) 
                    MakeParallax(info.ParallaxAmt);
                if (info.Smoke)
                    CreateSmoke(info.SmokeOffset, info.SmokeInBg);
                if (info.Mirror)
                    MakeMirror(text, info.MirrorKeepOffsetsClose);
                if (info.Floaty)
                    MakeFloaty();
                if (info.Banner)
                    MakeBanner(info.BannerSpeed, info.BannerAmplitude, info.BannerSliceSize, info.BannerSliceSinIncrement, info.BannerEaseDown, info.BannerOffset, info.BannerOnlyIfWindy);
                if (info.Bloom)
                    Add(new BloomPoint(info.BloomOffset, info.BloomAlpha, info.BloomRadius));
                if (info.Sound != null)
                    Add(new SoundSource(info.Sound));
                if (image == null) {
                    Add(image = new DecalImage());
                }
                Everest.Events.Decal.HandleDecalRegistry(this, info);
            }
        }
    }
    public static class DecalExt {

        public static Vector2 GetScale(this Decal self)
            => ((patch_Decal) self).Scale;
        public static void SetScale(this Decal self, Vector2 value)
            => ((patch_Decal) self).Scale = value;

    }
}
