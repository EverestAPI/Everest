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
                if (activeTextures.Count > 0) {
                    MTexture texture = activeTextures[(int) frame % activeTextures.Count];
                    Vector2 offset = new Vector2(texture.Center.X * Decal.scale.X % 1, texture.Center.Y * Decal.scale.X % 1);
                    texture.DrawCentered(Decal.Position + offset, Decal.Color, Decal.scale, Decal.Rotation);
                }
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
        [PatchMirrorMaskRender]
        public extern void MakeMirror(string path, bool keepOffsetsClose);

        [MonoModIgnore]
        [PatchMirrorMaskRender]
        private extern void MakeMirror(string path, Vector2 offset);

        [MonoModIgnore]
        [PatchMirrorMaskRender]
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
                        try {
                            DecalRegistry.PropertyHandlers[property.Key].Invoke(this, property.Value);
                        } catch (Exception e) {
                            patch_LevelEnter.ErrorMessage = Dialog.Get("postcard_decalregerror").Replace("((property))", property.Key).Replace("((decal))", text);
                            Logger.Log(LogLevel.Warn, "Decal Registry", $"Failed to apply property '{property.Key}' to {text}");
                            e.LogDetailed();
                        }

                    } else {
                        Logger.Log(LogLevel.Warn, "Decal Registry", $"Unknown property {property.Key} in decal {text}");
                    }
                }

                Everest.Events.Decal.HandleDecalRegistry(this, info);
                if (image == null) {
                    Add(image = new patch_DecalImage());
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

    /// <summary>
    /// Allow decal images to be rotated and correct rendering of images with non-even dimensions.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchDecalImageRender))]
    class PatchDecalImageRenderAttribute : Attribute { }

    /// <summary>
    /// Allow mirror masks to be rotated and correct rendering of masks with non-even dimensions.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchMirrorMaskRender))]
    class PatchMirrorMaskRenderAttribute : Attribute { }

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
            TypeDefinition t_Vector2 = MonoModRule.Modder.FindType("Microsoft.Xna.Framework.Vector2").Resolve();
            TypeDefinition t_Matrix = MonoModRule.Modder.FindType("Microsoft.Xna.Framework.Matrix").Resolve();

            // This is not allowed to be a TypeDefinition for ?? reasons
            TypeReference t_float = MonoModRule.Modder.Module.ImportReference(MonoModRule.Modder.FindType("System.Single").Resolve());
            
            FieldReference f_Decal_Rotation = t_Decal.FindField("Rotation");
            FieldReference f_Decal_Color = t_Decal.FindField("Color");
            FieldReference f_Decal_scale = t_Decal.FindField("scale");
            
            MethodReference m_get_Center = t_MTexture.FindProperty("Center").GetMethod;
            
            MethodReference m_Vector2_ctor = MonoModRule.Modder.Module.ImportReference(t_Vector2.FindMethod("System.Void .ctor(System.Single,System.Single)"));
            MethodReference m_Vector2_Transform = MonoModRule.Modder.Module.ImportReference(t_Vector2.FindMethod("Microsoft.Xna.Framework.Vector2 Transform(Microsoft.Xna.Framework.Vector2,Microsoft.Xna.Framework.Matrix)"));
            FieldReference f_Vector2_X = MonoModRule.Modder.Module.ImportReference(t_Vector2.FindField("X"));
            FieldReference f_Vector2_Y = MonoModRule.Modder.Module.ImportReference(t_Vector2.FindField("Y"));
            
            MethodReference m_Matrix_CreateRotationZ = MonoModRule.Modder.Module.ImportReference(t_Matrix.FindMethod("Microsoft.Xna.Framework.Matrix CreateRotationZ(System.Single)"));

            // These methods vary based on what class we're patching
            MethodReference m_get_Decal = null;
            MethodReference m_Draw_old = null;

            // Create some extra locals to make the math easier
            VariableDefinition v_vector = new VariableDefinition(MonoModRule.Modder.Module.ImportReference(t_Vector2));
            VariableDefinition v_X = new VariableDefinition(t_float);
            VariableDefinition v_Y = new VariableDefinition(t_float);
            context.Body.Variables.Add(v_vector);
            context.Body.Variables.Add(v_X);
            context.Body.Variables.Add(v_Y);

            ILCursor cursor = new ILCursor(context);

            // Part one: fix rendering for decals with non-integer centers (i.e. non-even dimensions) 
            
            // Jump to just after the decal texture is put on the stack.
            // Because the various decal components have different methods of finding their texture,
            // it's not possible to match the instruction that loads the texture itself.
            // Instead, we can just look for the instructions that put the decal position on the stack,
            // and put our cursor just before that, since the decal texture immediately precedes position
            // in the arguments being made to MTexture.Draw().
            // Also, we collect the MethodReference for the Decal getter for this component type.
            cursor.GotoNext(MoveType.Before,
                instr => instr.MatchLdarg(0),
                instr => instr.MatchCallvirt(out m_get_Decal),
                instr => instr.MatchLdfld("Monocle.Entity", "Position")
            );
            
            // Duplicate the texture reference on the stack so we leave a copy to be used as an argument to Draw().
            // Then get MTexture.Center, multiply it by a rotation matrix based on decal rotation, and store it in our Vector2 local.
            cursor.Emit(OpCodes.Dup);
            cursor.Emit(OpCodes.Call, m_get_Center);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Callvirt, m_get_Decal);
            cursor.Emit(OpCodes.Ldfld, f_Decal_Rotation);
            cursor.Emit(OpCodes.Ldc_R4, (float) Math.PI / 180);
            cursor.Emit(OpCodes.Mul);
            cursor.Emit(OpCodes.Call, m_Matrix_CreateRotationZ);
            cursor.Emit(OpCodes.Call, m_Vector2_Transform);
            cursor.Emit(OpCodes.Stloc_S, v_vector);
            
            // Get our stored Vector2 back, then get the X component as a float, and store the non-integer component
            // of that float in our first float local. This will be used to offset the "real" position when the decal
            // renders if the decal's Center is a non-integer. (If the Center is an integer, the offset will be 0.)
            cursor.Emit(OpCodes.Ldloc_S, v_vector);
            cursor.Emit(OpCodes.Ldfld, f_Vector2_X);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Callvirt, m_get_Decal);
            cursor.Emit(OpCodes.Ldfld, f_Decal_scale);
            cursor.Emit(OpCodes.Ldfld, f_Vector2_X);
            cursor.Emit(OpCodes.Mul);
            cursor.Emit(OpCodes.Ldc_R4, 1f);
            cursor.Emit(OpCodes.Rem);
            cursor.Emit(OpCodes.Stloc_S, v_X);
            
            // Same thing but for the Y.
            cursor.Emit(OpCodes.Ldloc_S, v_vector);
            cursor.Emit(OpCodes.Ldfld, f_Vector2_Y);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Callvirt, m_get_Decal);
            cursor.Emit(OpCodes.Ldfld, f_Decal_scale);
            cursor.Emit(OpCodes.Ldfld, f_Vector2_Y);
            cursor.Emit(OpCodes.Mul);
            cursor.Emit(OpCodes.Ldc_R4, 1f);
            cursor.Emit(OpCodes.Rem);
            cursor.Emit(OpCodes.Stloc_S, v_Y);
            
            // Now jump after the position has been put on the stack, so we can store and then modify it.
            cursor.GotoNext(MoveType.After,
                instr => instr.MatchLdfld("Monocle.Entity", "Position"));
            cursor.Emit(OpCodes.Stloc_S, v_vector);
            
            // Get our stored position back, get the position's X component, and add it to our calculated offset.
            cursor.Emit(OpCodes.Ldloc_S, v_vector);
            cursor.Emit(OpCodes.Ldfld, f_Vector2_X);
            cursor.Emit(OpCodes.Ldloc_S, v_X);
            cursor.Emit(OpCodes.Add);
            
            // And again for Y.
            cursor.Emit(OpCodes.Ldloc_S, v_vector);
            cursor.Emit(OpCodes.Ldfld, f_Vector2_Y);
            cursor.Emit(OpCodes.Ldloc_S, v_Y);
            cursor.Emit(OpCodes.Add);
            
            // Use our offsetted position values to create a new "adjusted" position, which will be used instead.
            cursor.Emit(OpCodes.Newobj, m_Vector2_ctor);
            
            // Part two: inject decal rotation
            
            // move to just after the decal's scale is obtained, but just before the draw call.
            // also get a reference to the draw call itself
            cursor.GotoNext(MoveType.After,
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

        public static void PatchMirrorMaskRender(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_Decal = MonoModRule.Modder.FindType("Celeste.Decal").Resolve();
            TypeDefinition t_MTexture = MonoModRule.Modder.FindType("Monocle.MTexture").Resolve();
            TypeDefinition t_Vector2 = MonoModRule.Modder.FindType("Microsoft.Xna.Framework.Vector2").Resolve();
            TypeDefinition t_Entity = MonoModRule.Modder.FindType("Monocle.Entity").Resolve();
            TypeDefinition t_Matrix = MonoModRule.Modder.FindType("Microsoft.Xna.Framework.Matrix").Resolve();

            FieldReference f_Decal_Rotation = t_Decal.FindField("Rotation");
            FieldReference f_Decal_scale = t_Decal.FindField("scale");

            FieldReference f_Entity_Position = t_Entity.FindField("Position");
            
            MethodReference m_get_Center = t_MTexture.FindProperty("Center").GetMethod;
            MethodDefinition m_DrawCentered = t_MTexture.FindMethod("System.Void DrawCentered(Microsoft.Xna.Framework.Vector2,Microsoft.Xna.Framework.Color,Microsoft.Xna.Framework.Vector2,System.Single)");

            MethodReference m_Vector2_ctor = MonoModRule.Modder.Module.ImportReference(t_Vector2.FindMethod("System.Void .ctor(System.Single,System.Single)"));
            MethodReference m_Vector2_Transform = MonoModRule.Modder.Module.ImportReference(t_Vector2.FindMethod("Microsoft.Xna.Framework.Vector2 Transform(Microsoft.Xna.Framework.Vector2,Microsoft.Xna.Framework.Matrix)"));
            FieldReference f_Vector2_X = MonoModRule.Modder.Module.ImportReference(t_Vector2.FindField("X"));
            FieldReference f_Vector2_Y = MonoModRule.Modder.Module.ImportReference(t_Vector2.FindField("Y"));
            
            MethodReference m_Matrix_CreateRotationZ = MonoModRule.Modder.Module.ImportReference(t_Matrix.FindMethod("Microsoft.Xna.Framework.Matrix CreateRotationZ(System.Single)"));
            
            MethodReference r_OnRender = null;

            ILCursor cursor = new ILCursor(context);

            // The mirror mask is drawn in a compiler-generated function, so we need to start a new context with it
            cursor.GotoNext(instr => instr.MatchLdftn(out r_OnRender));

            MethodDefinition m_OnRender = r_OnRender.Resolve();
            new ILContext(m_OnRender).Invoke(il => {
                // Some of the patched methods use closure locals so search for those
                FieldDefinition f_locals = m_OnRender.DeclaringType.FindField("CS$<>8__locals1");
                FieldReference f_this = null;
                FieldReference f_mask = null;

                ILCursor cursor = new ILCursor(il);
                
                // Copies the IL strategy from PatchDecalImageRotationAndCentering, modified to avoid adding locals
                
                cursor.GotoNext(MoveType.After,
                    instr => instr.MatchLdfld(out f_mask),
                    instr => instr.MatchLdarg(0)
                );

                cursor.GotoNext(MoveType.After,
                    instr => instr.MatchLdfld(out f_this),
                    instr => instr.MatchLdfld("Monocle.Entity", "Position")
                );
                
                cursor.Emit(OpCodes.Ldfld, f_Vector2_X);
                cursor.Emit(OpCodes.Ldarg_0);
                if (f_locals != null)
                    cursor.Emit(OpCodes.Ldfld, f_locals);
                cursor.Emit(OpCodes.Ldfld, f_mask);
                cursor.Emit(OpCodes.Call, m_get_Center);
                cursor.Emit(OpCodes.Ldarg_0);
                if (f_locals != null)
                    cursor.Emit(OpCodes.Ldfld, f_locals);
                cursor.Emit(OpCodes.Ldfld, f_this);
                cursor.Emit(OpCodes.Ldfld, f_Decal_Rotation);
                cursor.Emit(OpCodes.Ldc_R4, (float) Math.PI / 180);
                cursor.Emit(OpCodes.Mul);
                cursor.Emit(OpCodes.Call, m_Matrix_CreateRotationZ);
                cursor.Emit(OpCodes.Call, m_Vector2_Transform);
                cursor.Emit(OpCodes.Ldfld, f_Vector2_X);
                cursor.Emit(OpCodes.Ldarg_0);
                if (f_locals != null)
                    cursor.Emit(OpCodes.Ldfld, f_locals);
                cursor.Emit(OpCodes.Ldfld, f_this);
                cursor.Emit(OpCodes.Ldfld, f_Decal_scale);
                cursor.Emit(OpCodes.Ldfld, f_Vector2_X);
                cursor.Emit(OpCodes.Mul);
                cursor.Emit(OpCodes.Ldc_R4, 1f);
                cursor.Emit(OpCodes.Rem);
                cursor.Emit(OpCodes.Add);
                
                cursor.Emit(OpCodes.Ldarg_0);
                if (f_locals != null)
                    cursor.Emit(OpCodes.Ldfld, f_locals);
                cursor.Emit(OpCodes.Ldfld, f_this);
                cursor.Emit(OpCodes.Ldfld, f_Entity_Position);
                cursor.Emit(OpCodes.Ldfld, f_Vector2_Y);
                cursor.Emit(OpCodes.Ldarg_0);
                if (f_locals != null)
                    cursor.Emit(OpCodes.Ldfld, f_locals);
                cursor.Emit(OpCodes.Ldfld, f_mask);
                cursor.Emit(OpCodes.Call, m_get_Center);
                cursor.Emit(OpCodes.Ldarg_0);
                if (f_locals != null)
                    cursor.Emit(OpCodes.Ldfld, f_locals);
                cursor.Emit(OpCodes.Ldfld, f_this);
                cursor.Emit(OpCodes.Ldfld, f_Decal_Rotation);
                cursor.Emit(OpCodes.Ldc_R4, (float) Math.PI / 180);
                cursor.Emit(OpCodes.Mul);
                cursor.Emit(OpCodes.Call, m_Matrix_CreateRotationZ);
                cursor.Emit(OpCodes.Call, m_Vector2_Transform);
                cursor.Emit(OpCodes.Ldfld, f_Vector2_Y);
                cursor.Emit(OpCodes.Ldarg_0);
                if (f_locals != null)
                    cursor.Emit(OpCodes.Ldfld, f_locals);
                cursor.Emit(OpCodes.Ldfld, f_this);
                cursor.Emit(OpCodes.Ldfld, f_Decal_scale);
                cursor.Emit(OpCodes.Ldfld, f_Vector2_X);
                cursor.Emit(OpCodes.Mul);
                cursor.Emit(OpCodes.Ldc_R4, 1f);
                cursor.Emit(OpCodes.Rem);
                cursor.Emit(OpCodes.Add);
                
                cursor.Emit(OpCodes.Newobj, m_Vector2_ctor);

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
