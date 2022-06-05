#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0414 // The field is assigned but its value is never used

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Celeste {
    class patch_Decal : Decal {

        private Vector2 scale;
        public Vector2 Scale {
            get => scale;
            set => scale = value;
        }

#pragma warning disable CS0649 // field is never assigned and will always be null: it is initialized in vanilla code
        private List<MTexture> textures;
#pragma warning restore CS0649
        private bool scaredAnimal;

        private float hideRange;
        private float showRange;

        private Solid solid;
        private StaticMover staticMover;

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
            hideRange = 32f;
            showRange = 48f;

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

        [MonoModReplace]
        [MakeMethodPublic]
        public void MakeSolid(float x, float y, float w, float h, int surfaceSoundIndex, bool blockWaterfalls = true) {
            solid = new Solid(Position + new Vector2(x, y), w, h, true);
            solid.BlockWaterfalls = blockWaterfalls;
            solid.SurfaceSoundIndex = surfaceSoundIndex;
            Scene.Add(solid);
        }

        public void MakeCoreSwap(string coldPath, string hotPath) {
            Add(image = new CoreSwapImage(GFX.Game[coldPath], GFX.Game[hotPath]));
        }

        public void MakeStaticMover(int x, int y, int w, int h, bool jumpThrus = false) {
            staticMover = new StaticMover {
                SolidChecker = s => s.CollideRect(new Rectangle((int) X + x, (int) Y + y, w, h)),
                OnDestroy = () => {
                    RemoveSelf();
                    if (solid != null) 
                        solid.RemoveSelf(); 
                },
                OnDisable = () => {
                    Active = Visible = Collidable = false;
                    if (solid != null) 
                        solid.Collidable = false; 
                },
                OnEnable = () => {
                    Active = Visible = Collidable = true;
                    if (solid != null)
                        solid.Collidable = true;
                },
                OnMove = v => {
                    Position += v;
                    if (solid != null) {
                        if (staticMover.Platform != null) 
                            solid.LiftSpeed = staticMover.Platform.LiftSpeed;
                        solid.MoveHExact((int) v.X);
                        solid.MoveVExact((int) v.Y);
                    }
                },
                OnShake = v => { Position += v; },
            };
            if (jumpThrus)
                staticMover.JumpThruChecker = s => s.CollideRect(new Rectangle((int) X + x, (int) X + y, w, h));
            Add(staticMover);
        }

        public void MakeScaredAnimation(int hideRange, int showRange, int[] idleFrames, int[] hiddenFrames, int[] showFrames, int[] hideFrames) {
            Sprite sprite = (Sprite) (image = new Sprite(null, null));
            sprite.AddLoop("hidden", 0.1f, hiddenFrames.Select(i => textures[i]).ToArray());
            sprite.Add("return", 0.1f, "idle", showFrames.Select(i => textures[i]).ToArray());
            sprite.AddLoop("idle", 0.1f, idleFrames.Select(i => textures[i]).ToArray());
            sprite.Add("hide", 0.1f, "hidden", hideFrames.Select(i => textures[i]).ToArray());
            sprite.Play("idle", restart: true);
            sprite.Scale = scale;
            sprite.CenterOrigin();
            Add(sprite);
            this.hideRange = hideRange;
            this.showRange = showRange;
            scaredAnimal = true;
        }

        [MonoModIgnore]
        private Component image;

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

        [MonoModIgnore]
        [PatchDecalUpdate]
        public extern override void Update();
    }
    public static class DecalExt {

        public static Vector2 GetScale(this Decal self)
            => ((patch_Decal) self).Scale;
        public static void SetScale(this Decal self, Vector2 value)
            => ((patch_Decal) self).Scale = value;
    }
}
