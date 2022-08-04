#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0414 // The field is assigned but its value is never used

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

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

        private float frame;

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
        [MonoModPublic]
        public extern void MakeParallax(float amount);

        [MonoModIgnore]
        [MonoModPublic]
        public extern void CreateSmoke(Vector2 offset, bool inbg);

        [MonoModIgnore]
        [MonoModPublic]
        public extern void MakeMirror(string path, bool keepOffsetsClose);

        [MonoModIgnore]
        [MonoModPublic]
        public extern void MakeFloaty();

        [MonoModIgnore]
        [MonoModPublic]
        public extern void MakeBanner(float speed, float amplitude, int sliceSize, float sliceSinIncrement, bool easeDown, float offset = 0f, bool onlyIfWindy = false);

        [MonoModReplace]
        [MonoModPublic]
        public void MakeSolid(float x, float y, float w, float h, int surfaceSoundIndex, bool blockWaterfalls = true) {
            solid = new Solid(Position + new Vector2(x, y), w, h, true);
            solid.BlockWaterfalls = blockWaterfalls;
            solid.SurfaceSoundIndex = surfaceSoundIndex;
            Scene.Add(solid);
        }

        public void MakeSolid(float x, float y, float w, float h, int surfaceSoundIndex, bool blockWaterfalls = true, bool safe = true) {
            solid = new Solid(Position + new Vector2(x, y), w, h, safe);
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
                    solid?.RemoveSelf();
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
                OnAttach = p => {
                    p.Add(new EntityRemovedListener(() => RemoveSelf()));
                    CoreModule.Session.AttachedDecals.Add($"{Name}||{Position.X}||{Position.Y}");
                }
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

        public void RandomizeStartingFrame() {
            frame = Calc.Random.NextFloat(textures.Count);
        }

        public void MakeOverlay() {
            Add(new BeforeRenderHook(new Action(CreateOverlay)));
        }

        [MonoModIgnore]
        private Component image;

        public Component Image { get { return image; } set { image = value; } }

        public override void Awake(Scene scene) {
            base.Awake(scene);
            if (staticMover?.Platform == null && CoreModule.Session.AttachedDecals.Contains($"{Name}||{Position.X}||{Position.Y}")) {
                RemoveSelf();
            }
        }

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

                // Handle properties. Apply "scale" first since it affects other properties.
                foreach (KeyValuePair<string, XmlAttributeCollection> property in info.CustomProperties.OrderByDescending(p => p.Equals("scale"))) {
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

        private void CreateOverlay() {
            Tileset tileset = new Tileset(textures[0], 8, 8);
            for (int i = 0; i < textures[0].Width / 8; i++) {
                for (int j = 0; j < textures[0].Height / 8; j++) {
                    TileInterceptor.TileCheck(Scene, tileset[i, j], new Vector2(Position.X - textures[0].Center.X + i * 8, Position.Y - textures[0].Center.Y + j * 8));
                }
            }
            RemoveSelf();
        }
    }
    public static class DecalExt {

        public static Vector2 GetScale(this Decal self)
            => ((patch_Decal) self).Scale;
        public static void SetScale(this Decal self, Vector2 value)
            => ((patch_Decal) self).Scale = value;
    }
}

namespace MonoMod {
    /// <summary>
    /// Un-hardcode the range of the "Scared" decals.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchDecalUpdate))]
    class PatchDecalUpdateAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchDecalUpdate(ILContext context, CustomAttribute attrib) {
            FieldDefinition f_hideRange = context.Method.DeclaringType.FindField("hideRange");
            FieldDefinition f_showRange = context.Method.DeclaringType.FindField("showRange");

            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(instr => instr.MatchLdcR4(32f));
            cursor.Remove();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_hideRange);

            cursor.GotoNext(instr => instr.MatchLdcR4(48f));
            cursor.Remove();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_showRange);
        }

    }
}
