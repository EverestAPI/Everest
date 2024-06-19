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
using MonoMod.InlineRT;
using Celeste.Mod.Helpers;

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

        public float Rotation = 0f;

        public Color Color;

        public bool DepthSetByPlacement;

        private bool scaredAnimal;

        private float hideRange;
        private float showRange;

        private float frame;

        private List<Solid> solids;

        private StaticMover staticMover;

        public patch_Decal(string texture, Vector2 position, Vector2 scale, int depth)
            : base(texture, position, scale, depth) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        private class patch_Banner : Component {

            public patch_Banner()
                : base(active: false, visible: true) {
                // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            }

            [MonoModIgnore]
            [PatchDecalImageRender]
            public extern override void Render();

        }

        private class patch_DecalImage : Component {

            public patch_DecalImage()
                : base(active: false, visible: true) {
                // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            }

            [MonoModIgnore]
            [PatchDecalImageRender]
            public extern override void Render();

        }

        private class patch_CoreSwapImage : Component {

            public patch_CoreSwapImage(MTexture hot, MTexture cold) : base(active: false, visible: true) {
                // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
            }

            [MonoModIgnore]
            [PatchDecalImageRender]
            public extern override void Render();

        }

        private class FlagSwapImage : Component {
            private string flag;
            private List<MTexture> off;
            private List<MTexture> on;
            private List<MTexture> activeTextures => (Scene as Level).Session.GetFlag(flag) ? on : off;
            private int loopCount;
            private float frame;
            public patch_Decal Decal => (patch_Decal) Entity;

            public FlagSwapImage(string flag, List<MTexture> off, List<MTexture> on) : base(active: true, visible: true) {
                this.flag = flag;
                this.off = off;
                this.on = on;
                loopCount = Math.Max(off.Count, 1) * Math.Max(on.Count, 1);
            }

            public override void Update() {
                frame = (frame + Decal.AnimationSpeed * Engine.DeltaTime) % loopCount;
            }

            public override void Render() {
                if (activeTextures.Count > 0)
                    activeTextures[(int) frame % activeTextures.Count].DrawCentered(Decal.Position, Decal.Color, Decal.scale, Decal.Rotation);
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
            solids = new List<Solid>();
            Color = Color.White;

            orig_ctor(texture, position, scale, depth);
        }

        [MonoModConstructor]
        public void ctor(string texture, Vector2 position, Vector2 scale, int depth, float rotation, Color color) {
            ctor(texture, position, scale, depth);
            Rotation = MathHelper.ToRadians(rotation);
            Color = color;
        }

        [MonoModConstructor]
        public void ctor(string texture, Vector2 position, Vector2 scale, int depth, float rotation, string color_hex) {
            ctor(texture, position, scale, depth, rotation, patch_Calc.HexToColorWithAlpha(color_hex));
        }

        [MonoModIgnore]
        [MonoModPublic]
        public extern void MakeParallax(float amount);

        [MonoModIgnore]
        [MonoModPublic]
        public extern void CreateSmoke(Vector2 offset, bool inbg);

        [MonoModIgnore]
        [MonoModPublic]
        [PatchMirrorMaskRotation]
        public extern void MakeMirror(string path, bool keepOffsetsClose);

        [MonoModIgnore]
        [PatchMirrorMaskRotation]
        private extern void MakeMirror(string path, Vector2 offset);

        [MonoModIgnore]
        [PatchMirrorMaskRotation]
        private extern void MakeMirrorSpecialCase(string path, Vector2 offset);

        [MonoModIgnore]
        [MonoModPublic]
        public extern void MakeFloaty();

        [MonoModIgnore]
        [MonoModPublic]
        public extern void MakeBanner(float speed, float amplitude, int sliceSize, float sliceSinIncrement, bool easeDown, float offset = 0f, bool onlyIfWindy = false);

        [MonoModReplace]
        [MonoModPublic]
        public void MakeSolid(float x, float y, float w, float h, int surfaceSoundIndex, bool blockWaterfalls = true) {
            Solid solid = new Solid(Position + new Vector2(x, y), w, h, safe: true) {
                BlockWaterfalls = blockWaterfalls,
                SurfaceSoundIndex = surfaceSoundIndex,
            };
            solids.Add(solid);
            Scene.Add(solid);
        }

        public void MakeSolid(float x, float y, float w, float h, int surfaceSoundIndex, bool blockWaterfalls = true, bool safe = true) {
            Solid solid = new Solid(Position + new Vector2(x, y), w, h, safe) {
                BlockWaterfalls = blockWaterfalls,
                SurfaceSoundIndex = surfaceSoundIndex,
            };
            solids.Add(solid);
            Scene.Add(solid);
        }

        [Obsolete("Use MakeFlagSwap with the cold flag instead.")]
        public void MakeCoreSwap(string coldPath, string hotPath) {
            Add(image = new patch_CoreSwapImage(GFX.Game[coldPath], GFX.Game[hotPath]));
        }

        public void MakeFlagSwap(string flag, string offPath, string onPath) {
            Add(image = new FlagSwapImage(flag, offPath != null ? GFX.Game.GetAtlasSubtextures(offPath) : new List<MTexture>(),
                                                onPath != null ? GFX.Game.GetAtlasSubtextures(onPath) : new List<MTexture>()));
        }

        public void MakeStaticMover(int x, int y, int w, int h, bool jumpThrus = false) {
            staticMover = new StaticMover {
                SolidChecker = s => !solids.Contains(s) && s.CollideRect(new Rectangle((int) X + x, (int) Y + y, w, h)),
                OnDestroy = () => {
                    RemoveSelf();
                    solids.ForEach(s => s.RemoveSelf());
                },
                OnDisable = () => {
                    Active = Visible = Collidable = false;
                    solids.ForEach(s => s.Collidable = false);
                },
                OnEnable = () => {
                    Active = Visible = Collidable = true;
                    solids.ForEach(s => s.Collidable = true);
                },
                OnMove = v => {
                    Position += v;
                    Vector2 liftSpeed = staticMover.Platform.LiftSpeed;
                    solids.ForEach(s => {
                        s.MoveH(v.X, liftSpeed.X);
                        s.MoveV(v.Y, liftSpeed.Y);
                    });
                },
                OnShake = v => Position += v,
                OnAttach = p => {
                    p.Add(new EntityRemovedListener(() => {
                        RemoveSelf();
                        solids.ForEach(s => s.RemoveSelf());
                    }));
                    CoreModule.Session.AttachedDecals.Add(string.Format("{0}||{1}||{2}", Name, Position.X, Position.Y));
                }
            };
            if (jumpThrus)
                staticMover.JumpThruChecker = s => s.CollideRect(new Rectangle((int) X + x, (int) X + y, w, h));
            Add(staticMover);
        }

        public void MakeAnimation(int[] frames) {
            textures = frames.Select(i => textures[i]).ToList();
        }

        public void MakeScaredAnimation(int hideRange, int showRange, int[] idleFrames, int[] hiddenFrames, int[] showFrames, int[] hideFrames) {
            Sprite sprite = (Sprite) (image = new Sprite(null, null));
            sprite.AddLoop("hidden", 0.1f, hiddenFrames.Select(i => textures[i]).ToArray());
            sprite.Add("return", 0.1f, "idle", showFrames.Select(i => textures[i]).ToArray());
            sprite.AddLoop("idle", 0.1f, idleFrames.Select(i => textures[i]).ToArray());
            sprite.Add("hide", 0.1f, "hidden", hideFrames.Select(i => textures[i]).ToArray());
            sprite.Play("idle", restart: true);
            sprite.Scale = scale;
            sprite.Rotation = Rotation;
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
            if (staticMover?.Platform == null && CoreModule.Session.AttachedDecals.Contains(string.Format("{0}||{1}||{2}", Name, Position.X, Position.Y))) {
                RemoveSelf();
            }
        }

        public extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            int depth = Depth;

            orig_Added(scene);

            if (DepthSetByPlacement)
                Depth = depth;

            // Handle the Decal Registry
            string text = Name.ToLower();
            if (text.StartsWith("decals/", StringComparison.Ordinal)) {
                text = text.Substring(7);
            }

            if (!DecalRegistry.RegisteredDecals.TryGetValue(text, out DecalRegistry.DecalInfo info))
                return;
            
            Remove(image);
            image = null;
            
            // apply all decal registry handlers.
            foreach (var handler in info.Handlers) {
                try {
                    handler.ApplyTo(this);
                } catch (Exception e) {
                    patch_LevelEnter.ErrorMessage = Dialog.Get("postcard_decalregerror")
                        .Replace("((property))", handler.Name)
                        .Replace("((decal))", text);
                    Logger.Log(LogLevel.Warn, "Decal Registry", $"Failed to apply property '{handler.Name}' to {text}");
                    e.LogDetailed();
                }
            }

            Everest.Events.Decal.HandleDecalRegistry(this, info);
            if (image == null) {
                Add(image = new patch_DecalImage());
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

        [Obsolete("Use Decal.Scale instead.")]
        public static Vector2 GetScale(this Decal self)
            => ((patch_Decal) self).Scale;
        [Obsolete("Use Decal.Scale instead.")]
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

    /// <summary>
    /// Allow decal images to be rotated.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchDecalImageRender))]
    class PatchDecalImageRenderAttribute : Attribute { }

    /// <summary>
    /// Allow mirror masks to be rotated.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchMirrorMaskRotation))]
    class PatchMirrorMaskRotationAttribute : Attribute { }

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

        public static void PatchDecalImageRender(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_Decal = MonoModRule.Modder.FindType("Celeste.Decal").Resolve();
            TypeDefinition t_MTexture = MonoModRule.Modder.FindType("Monocle.MTexture").Resolve();

            FieldReference f_Decal_Rotation = t_Decal.FindField("Rotation");
            FieldReference f_Decal_Color = t_Decal.FindField("Color");

            MethodReference m_get_Decal = null;
            MethodReference m_Draw_old = null;

            ILCursor cursor = new ILCursor(context);

            // move to just after the decal's scale is obtained, but just before the draw call.
            // also get references to the Decal getter and to the draw call itself
            cursor.GotoNext(MoveType.After,
                            instr => instr.MatchCallvirt(out m_get_Decal),
                            instr => instr.MatchLdfld("Celeste.Decal", "scale"),
                            instr => instr.MatchCallvirt(out m_Draw_old));
            cursor.Index--;

            // find an appropriate draw method; it should have the same signature, but also take a float for the rotation as its last argument
            MethodReference m_Draw_new = t_MTexture.FindMethod($"{m_Draw_old.FullName.TrimEnd(')')},System.Single)");

            // load the rotation from the decal
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Callvirt, m_get_Decal);
            cursor.Emit(OpCodes.Ldfld, f_Decal_Rotation);
            // ...and replace the draw call to accept it
            cursor.Emit(OpCodes.Callvirt, m_Draw_new);
            cursor.Remove();

            // go back to the start
            cursor.Index = 0;

            // move to just before the colour white is obtained, and replace it with some other colour
            cursor.GotoNext(MoveType.Before, instr => instr.MatchCallOrCallvirt(out MethodReference method)
                                                   && method.FullName == "Microsoft.Xna.Framework.Color Microsoft.Xna.Framework.Color::get_White()");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Callvirt, m_get_Decal);
            cursor.Emit(OpCodes.Ldfld, f_Decal_Color);
            cursor.Remove();
        }

        public static void PatchMirrorMaskRotation(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_MTexture = MonoModRule.Modder.FindType("Monocle.MTexture").Resolve();
            FieldDefinition f_Decal_Rotation = context.Method.DeclaringType.FindField("Rotation");
            MethodDefinition m_DrawCentered = t_MTexture.FindMethod("System.Void DrawCentered(Microsoft.Xna.Framework.Vector2,Microsoft.Xna.Framework.Color,Microsoft.Xna.Framework.Vector2,System.Single)");
            MethodReference r_OnRender = null;

            ILCursor cursor = new ILCursor(context);

            // The mirror mask is drawn in a compiler-generated function, so we need to start a new context with it
            cursor.GotoNext(instr => instr.MatchLdftn(out r_OnRender));

            MethodDefinition m_OnRender = r_OnRender.Resolve();
            new ILContext(m_OnRender).Invoke(il => {
                // Some of the patched methods use closure locals so search for those
                FieldDefinition f_locals = m_OnRender.DeclaringType.FindField("CS$<>8__locals1");
                FieldReference f_this = null;

                ILCursor cursor = new ILCursor(il);

                // Grab the function's decal reference and move to just before the draw call
                cursor.GotoNext(MoveType.After,
                    instr => instr.MatchLdfld(out f_this),
                    instr => instr.MatchLdfld("Celeste.Decal", "scale"));

                // Load the rotation field (use closure locals if found), call our new draw function, and then remove the old one
                cursor.Emit(OpCodes.Ldarg_0);
                if (f_locals != null)
                    cursor.Emit(OpCodes.Ldfld, f_locals);
                cursor.Emit(OpCodes.Ldfld, f_this);
                cursor.Emit(OpCodes.Ldfld, f_Decal_Rotation);
                cursor.Emit(OpCodes.Callvirt, m_DrawCentered);
                cursor.Remove();
            });
        }
    }
}
