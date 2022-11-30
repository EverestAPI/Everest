using Celeste;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using System;
using System.Collections;

namespace Celeste {
    class patch_DreamBlock : DreamBlock {
        private bool playerHasDreamDash;
        private LightOcclude occlude;
        private float whiteHeight;
        private float whiteFill;
        private Shaker shaker;
        private Vector2 shake;
        private int randomSeed = Calc.Random.Next();

        public patch_DreamBlock(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModLinkTo("Celeste.DreamBlock", "System.Void .ctor(Microsoft.Xna.Framework.Vector2,System.Single,System.Single,System.Nullable`1<Microsoft.Xna.Framework.Vector2>,System.Boolean,System.Boolean,System.Boolean)")]
        [MonoModForceCall]
        [MonoModRemove]
        public extern void ctor(Vector2 position, float width, float height, Vector2? node, bool fastMoving, bool oneUse, bool below);
        [MonoModConstructor]
        public void ctor(Vector2 position, float width, float height, Vector2? node, bool fastMoving, bool oneUse) {
            ctor(position, width, height, node, fastMoving, oneUse, false);
        }

        [MonoModIgnore]
        [PatchDreamBlockSetup]
        public new extern void Setup();

        public void DeactivateNoRoutine() {
            if (playerHasDreamDash) {
                playerHasDreamDash = false;

                Setup();

                if (occlude == null) {
                    occlude = new LightOcclude(1f);
                }
                Add(occlude);

                whiteHeight = 1f;
                whiteFill = 0f;

                if (shaker != null) {
                    shaker.On = false;
                }

                SurfaceSoundIndex = 11;
            }
        }

        public IEnumerator Deactivate() {
            Level level = SceneAs<Level>();
            yield return 1f;

            Input.Rumble(RumbleStrength.Light, RumbleLength.Long);
            if (shaker == null) {
                shaker = new Shaker(true, t => {
                    shake = t;
                });
            }
            Add(shaker);
            shaker.Interval = 0.02f;
            shaker.On = true;
            for (float alpha = 0f; alpha < 1f; alpha += Engine.DeltaTime) {
                whiteFill = Ease.CubeIn(alpha);
                yield return null;
            }
            shaker.On = false;
            yield return 0.5f;

            DeactivateNoRoutine();

            whiteHeight = 1f;
            whiteFill = 1f;
            for (float yOffset = 1f; yOffset > 0f; yOffset -= Engine.DeltaTime * 0.5f) {
                whiteHeight = yOffset;
                if (level.OnInterval(0.1f)) {
                    for (int xOffset = 0; xOffset < Width; xOffset += 4) {
                        level.ParticlesFG.Emit(Strawberry.P_WingsBurst, new Vector2(X + xOffset, Y + Height * whiteHeight + 1f));
                    }
                }
                if (level.OnInterval(0.1f)) {
                    level.Shake(0.3f);
                }
                Input.Rumble(RumbleStrength.Strong, RumbleLength.Short);
                yield return null;
            }
            while (whiteFill > 0f) {
                whiteFill -= Engine.DeltaTime * 3f;
                yield return null;
            }
        }

        public IEnumerator FastDeactivate() {
            Level level = SceneAs<Level>();
            yield return null;

            Input.Rumble(RumbleStrength.Light, RumbleLength.Short);
            if (shaker == null) {
                shaker = new Shaker(true, t => {
                    shake = t;
                });
            }
            Add(shaker);
            shaker.Interval = 0.02f;
            shaker.On = true;
            for (float alpha = 0f; alpha < 1f; alpha += Engine.DeltaTime * 3f) {
                whiteFill = Ease.CubeIn(alpha);
                yield return null;
            }
            shaker.On = false;
            yield return 0.1f;

            DeactivateNoRoutine();

            whiteHeight = 1f;
            whiteFill = 1f;

            level.ParticlesFG.Emit(Strawberry.P_WingsBurst, (int) Width, TopCenter, Vector2.UnitX * Width / 2, Color.White, (float) Math.PI);
            level.ParticlesFG.Emit(Strawberry.P_WingsBurst, (int) Width, BottomCenter, Vector2.UnitX * Width / 2, Color.White, 0);
            level.ParticlesFG.Emit(Strawberry.P_WingsBurst, (int) Height, CenterLeft, Vector2.UnitY * Height / 2, Color.White, (float) Math.PI * 1.5f);
            level.ParticlesFG.Emit(Strawberry.P_WingsBurst, (int) Height, CenterRight, Vector2.UnitY * Height / 2, Color.White, (float) Math.PI / 2);
            level.Shake(0.3f);
            yield return 0.1f;

            while (whiteFill > 0f) {
                whiteFill -= Engine.DeltaTime * 3f;
                yield return null;
            }
        }

        public IEnumerator FastActivate() {
            Level level = SceneAs<Level>();
            yield return null;

            Input.Rumble(RumbleStrength.Light, RumbleLength.Short);
            if (shaker == null) {
                shaker = new Shaker(true, t => {
                    shake = t;
                });
            }
            Add(shaker);
            shaker.Interval = 0.02f;
            shaker.On = true;
            for (float alpha = 0f; alpha < 1f; alpha += Engine.DeltaTime * 3f) {
                whiteFill = Ease.CubeIn(alpha);
                yield return null;
            }
            shaker.On = false;
            yield return 0.1f;

            ActivateNoRoutine();

            whiteHeight = 1f;
            whiteFill = 1f;

            level.ParticlesFG.Emit(Strawberry.P_WingsBurst, (int) Width, TopCenter, Vector2.UnitX * Width / 2, Color.White, (float) Math.PI);
            level.ParticlesFG.Emit(Strawberry.P_WingsBurst, (int) Width, BottomCenter, Vector2.UnitX * Width / 2, Color.White, 0);
            level.ParticlesFG.Emit(Strawberry.P_WingsBurst, (int) Height, CenterLeft, Vector2.UnitY * Height / 2, Color.White, (float) Math.PI * 1.5f);
            level.ParticlesFG.Emit(Strawberry.P_WingsBurst, (int) Height, CenterRight, Vector2.UnitY * Height / 2, Color.White, (float) Math.PI / 2);
            level.Shake(0.3f);
            yield return 0.1f;

            while (whiteFill > 0f) {
                whiteFill -= Engine.DeltaTime * 3f;
                yield return null;
            }
        }

        [MonoModReplace]
        private Vector2 PutInside(Vector2 pos) {
            // vanilla used loops here to move the particle inside the dream block step by step,
            // which can decrease the performance when the dream block is very far from (0, 0)
            if (pos.X > Right) {
                pos.X -= (float) Math.Ceiling((pos.X - Right) / Width) * Width;
            } else if (pos.X < Left) {
                pos.X += (float) Math.Ceiling((Left - pos.X) / Width) * Width;
            }
            if (pos.Y > Bottom) {
                pos.Y -= (float) Math.Ceiling((pos.Y - Bottom) / Height) * Height;
            } else if (pos.Y < Top) {
                pos.Y += (float) Math.Ceiling((Top - pos.Y) / Height) * Height;
            }
            return pos;
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches <see cref="DreamBlock.Setup()" /> to fix issue #556.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchDreamBlockSetup))]
    class PatchDreamBlockSetupAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchDreamBlockSetup(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);

            // find the (int) cast that's used as the particles array length
            cursor.GotoNext(
                instr => instr.MatchConvI4(),
                instr => instr.MatchNewarr(out _),
                instr => instr.MatchStfld(out _));

            // insert a tiny subtraction to ensure that values slightly less than a whole number will be floored 
            cursor.Emit(OpCodes.Ldc_R4, 0.0001f);
            cursor.Emit(OpCodes.Sub);
        }

    }
}
