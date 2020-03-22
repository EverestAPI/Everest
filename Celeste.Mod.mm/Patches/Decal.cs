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
        [MakeMethodPublic]
        public extern void MakeParallax(float amount);

        [MonoModIgnore]
        [MakeMethodPublic]
        public extern void CreateSmoke(Vector2 offset, bool inbg);

        [MonoModIgnore]
        [MakeMethodPublic]
        public extern void MakeMirror(string path, bool keepOffsetsClose);

        [MonoModIgnore]
        [MakeMethodPublic]
        public extern void MakeFloaty();

        [MonoModIgnore]
        [MakeMethodPublic]
        public extern void MakeBanner(float speed, float amplitude, int sliceSize, float sliceSinIncrement, bool easeDown, float offset = 0f, bool onlyIfWindy = false);

        [MonoModIgnore]
        private Component image;

        public void MakeCoreSwap(string coldPath, string hotPath) {
            Add(image = new CoreSwapImage(GFX.Game[coldPath], GFX.Game[hotPath]));
        }

        public Component Image { get { return image; } set { image = value; } }

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

                // Handle properties
                foreach (KeyValuePair<string, XmlAttributeCollection> property in info.CustomProperties) {
                    if (DecalRegistry.PropertyHandlers.ContainsKey(property.Key)) {
                        DecalRegistry.PropertyHandlers[property.Key].Invoke(this, property.Value);
                    } else {
                        Logger.Log(LogLevel.Warn, "Decal Registry", $"Unknown property {property.Key} in decal {text}");
                    }
                }

                Everest.Events.Decal.HandleDecalRegistry(this, info);
                if (image == null) {
                    Add(image = new DecalImage());
                }

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
