#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    class patch_Player : Player {

        // We're effectively in Player, but still need to "expose" private fields to our mod.
        private bool wasDashB;
        private HashSet<Trigger> triggersInside;
        private List<Entity> temp;

        private static int diedInGBJ = 0;
        private int framesAlive;
        private Level level;

        public IntroTypes? OverrideIntroType;

        public new int MaxDashes {
            get {
                if (SaveData.Instance.Assists.DashMode != Assists.DashModes.Normal && level?.InCutscene == false) {
                    return 2;
                }

                return Inventory.Dashes;
            }
        }


        public bool IsIntroState {
            get {
                int state = StateMachine.State;
                return StIntroWalk <= state && state <= StIntroWakeUp;
            }
        }

        public patch_Player(Vector2 position, PlayerSpriteMode spriteMode)
            : base(position, spriteMode) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [MonoModConstructor]
        [PatchPlayerCtorOnFrameChange]
        public extern void ctor(Vector2 position, PlayerSpriteMode spriteMode);

        public extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            if (OverrideIntroType != null) {
                IntroType = OverrideIntroType.Value;
                OverrideIntroType = null;
            }

            orig_Added(scene);

            framesAlive = int.MaxValue;

            if (Scene is Level) {
                framesAlive = 0;
            }

            Everest.Events.Player.Spawn(this);
        }

        [MonoModReplace]
        private void CreateTrail() {
            Vector2 scale = new Vector2(Math.Abs(Sprite.Scale.X) * (float) Facing, Sprite.Scale.Y);
            TrailManager.Add(this, scale, GetCurrentTrailColor());
        }

        [PatchPlayerOrigUpdate] // Manipulate the method via MonoModRules
        public extern void orig_Update();
        public override void Update() {
            orig_Update();

            Level level = Scene as Level;
            if (level == null)
                return;
            if (level.CanPause && framesAlive < int.MaxValue)
                framesAlive++;
            if (framesAlive >= 8)
                diedInGBJ = 0;
        }

        public bool _IsOverWater() {
            // check if we are 2 pixels over water (or less).
            Rectangle bounds = Collider.Bounds;
            bounds.Height += 2;
            return Scene.CollideCheck<Water>(bounds);
        }

        private extern void orig_UpdateSprite();
        private void UpdateSprite() {
            orig_UpdateSprite();

            // Don't slow down the sprite (f.e. in space) for certain states,
            // as their animations may become unbearably long
            // or desynced from their sounds and thus broken.
            if (StateMachine.State == StIntroWakeUp ||
                StateMachine.State == StStarFly) {
                Sprite.Rate = Sprite.Rate < 0f ? -1f : 1f;
            }
        }

        public extern PlayerDeadBody orig_Die(Vector2 direction, bool evenIfInvincible, bool registerDeathInStats);
        public new PlayerDeadBody Die(Vector2 direction, bool evenIfInvincible = false, bool registerDeathInStats = true) {
            Level level = Scene as Level;
            PlayerDeadBody body = orig_Die(direction, evenIfInvincible, registerDeathInStats);

            if (body != null) {
                Everest.Events.Player.Die(this);
                // 2 catches spawn-blade-kill GBJs.
                // 4 catches spawn-OOB-kill GBJs.
                if (framesAlive < 6 && level != null) {
                    diedInGBJ++;
                    if (diedInGBJ != 0 && (diedInGBJ % 2) == 0 && level.Session.Area.GetLevelSet() != "Celeste" && !CoreModule.Settings.DisableAntiSoftlock) {
                        level.Pause();
                        return null;
                    }
                }
            }

            return body;
        }

        private extern void orig_BoostBegin();
        private void BoostBegin() {
            if (SceneAs<Level>()?.Session.MapData.GetMeta()?.TheoInBubble ?? false) {
                RefillDash();
                RefillStamina();
            } else {
                orig_BoostBegin();
            }
        }

        private extern void orig_WindMove(Vector2 move);
        private void WindMove(Vector2 move) {
            // Don't apply wind on player in the Attract state: this would constantly push the player away from its target.
            // This causes an infinite loop when hitting Badeline bosses.
            if (StateMachine.State != StAttract)
                orig_WindMove(move);
        }

        [MonoModIgnore]
        [PatchPlayerOnCollideV]
        private extern void OnCollideV(CollisionData data);

        [MonoModIgnore]
        [PatchPlayerClimbBegin]
        private extern void ClimbBegin();

        [MonoModIgnore]
        [PatchPlayerOrigWallJump]
        private extern void orig_WallJump(int dir);
        private void WallJump(int dir) {
            if ((Scene as Level).Session.Area.GetLevelSet() != "Celeste") {
                // Fix vertical boost from upwards-moving solids not being applied correctly when dir != -1
                if (LiftSpeed == Vector2.Zero) {
                    Solid solid = CollideFirst<Solid>(Position + Vector2.UnitX * 3f * -dir);
                    if (solid != null) {
                        LiftSpeed = solid.LiftSpeed;
                    }
                }
            }
            orig_WallJump(dir);
        }

        /// <summary>
        /// Get the current player dash trail color.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Color GetCurrentTrailColor() => GetTrailColor(wasDashB);
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Color GetTrailColor(bool wasDashB) {
            if (Sprite.Mode == PlayerSpriteMode.MadelineAsBadeline)
                return wasDashB ? NormalBadelineHairColor : UsedBadelineHairColor;
            return wasDashB ? NormalHairColor : UsedHairColor;
        }

        public Vector2 ExplodeLaunch(Vector2 from, bool snapUp = true) {
            return ExplodeLaunch(from, snapUp, false);
        }

        private extern bool orig_Pickup(Holdable pickup);
        private bool Pickup(Holdable pickup) {
            // Madeline cannot grab something if she is dead...
            // this causes frame-perfect crashes when grabbing a jelly and getting killed at the same time.
            if (Dead) {
                return false;
            }

            return orig_Pickup(pickup);
        }

        public override void SceneBegin(Scene scene) {
            base.SceneBegin(scene);
            diedInGBJ = 0;
        }

        public extern void orig_SceneEnd(Scene scene);
        public override void SceneEnd(Scene scene) {
            orig_SceneEnd(scene);

            // if we are not entering PICO-8 or the Reflection Fall cutscene...
            if (!(patch_Engine.NextScene is Pico8.Emulator) && !(patch_Engine.NextScene is OverworldReflectionsFall)) {
                // make sure references to the previous level don't leak if hot reloading inside of a trigger.
                triggersInside?.Clear();
                temp?.Clear();
                level = null;
            }
        }

        [MonoModIgnore]
        [PatchPlayerBeforeUpTransition]
        public new extern void BeforeUpTransition();

        [MonoModIgnore]
        [PatchPlayerStarFlyReturnToNormalHitbox]
        private extern void StarFlyReturnToNormalHitbox();
    }
    public static class PlayerExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <inheritdoc cref="patch_Player.GetCurrentTrailColor"/>
        public static Color GetCurrentTrailColor(this Player self)
            => ((patch_Player) self).GetCurrentTrailColor();

        /// <summary>
        /// Get whether the player is in an intro state or not.
        /// </summary>
        public static bool IsIntroState(this Player self)
            => ((patch_Player) self).IsIntroState;

    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the orig_Update method in Player instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchPlayerOrigUpdate))]
    class PatchPlayerOrigUpdateAttribute : Attribute { }

    /// <summary>
    /// Patches the method to only set the player Speed.X if not in the RedDash state.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchPlayerBeforeUpTransition))]
    class PatchPlayerBeforeUpTransition : Attribute { }

    /// <summary>
    /// Patches the method to kill the player instead of crashing when exiting a feather in a solid.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchPlayerStarFlyReturnToNormalHitbox))]
    class PatchPlayerStarFlyReturnToNormalHitboxAttribute : Attribute { }

    /// <summary>
    /// Patches the method to un-hardcode the FMOD event string used to play the footstep/landing sound effect.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchPlayerOnCollideV))]
    class PatchPlayerOnCollideV : Attribute { }

    /// <summary>
    /// Patches the method to un-hardcode the FMOD event string used to play the grab sound effect.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchPlayerClimbBegin))]
    class PatchPlayerClimbBegin : Attribute { }

    /// <summary>
    /// Patches the method to un-hardcode the FMOD event string used to play the walljump sound effect.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchPlayerOrigWallJump))]
    class PatchPlayerOrigWallJumpAttribute : Attribute { }

    /// <summary>
    /// Patches the method to un-hardcode the FMOD event string used to play the footstep and grab sound effect.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchPlayerCtorOnFrameChange))]
    class PatchPlayerCtorOnFrameChangeAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchPlayerOrigUpdate(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_IsOverWater = context.Method.DeclaringType.FindMethod("System.Boolean _IsOverWater()");

            Mono.Collections.Generic.Collection<Instruction> instrs = context.Body.Instructions;
            ILProcessor il = context.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count - 4; instri++) {
                // turn "if (Speed.Y < 0f && Speed.Y >= -60f)" into "if (Speed.Y < 0f && Speed.Y >= -60f && _IsOverWater())"

                // 0: ldarg.0
                // 1: ldflda Celeste.Player::Speed
                // 2: ldfld Vector2::Y
                // 3: ldc.r4 -60
                // 4: blt.un [instruction after if]
                // 5: ldarg.0
                // 6: call Player::_IsOverWater
                // 7: brfalse [instruction after if]
                if (instrs[instri].OpCode == OpCodes.Ldarg_0
                    && instrs[instri + 1].MatchLdflda("Celeste.Player", "Speed")
                    && instrs[instri + 2].MatchLdfld("Microsoft.Xna.Framework.Vector2", "Y")
                    && instrs[instri + 3].MatchLdcR4(-60f)
                    && instrs[instri + 4].OpCode == OpCodes.Blt_Un
                ) {
                    instrs.Insert(instri + 5, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri + 6, il.Create(OpCodes.Call, m_IsOverWater));
                    instrs.Insert(instri + 7, il.Create(OpCodes.Brfalse, instrs[instri + 4].Operand));
                }
            }
        }

        public static void PatchPlayerBeforeUpTransition(ILContext context, CustomAttribute attrib) {
            FieldDefinition f_Player_StateMachine = context.Method.DeclaringType.FindField("StateMachine");
            MethodDefinition m_StateMachine_get_State = f_Player_StateMachine.FieldType.Resolve().FindMethod("System.Int32 get_State()");

            ILCursor cursor = new ILCursor(context);

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_Player_StateMachine);
            cursor.Emit(OpCodes.Callvirt, m_StateMachine_get_State);
            cursor.Emit(OpCodes.Ldc_I4_5);
            Instruction target = cursor.Clone().GotoNext(instr => instr.OpCode == OpCodes.Ldarg_0, instr => instr.MatchLdfld("Celeste.Player", "StateMachine")).Next;
            cursor.Emit(OpCodes.Beq_S, target);
        }

        public static void PatchPlayerStarFlyReturnToNormalHitbox(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_Vector2 = MonoModRule.Modder.FindType("Microsoft.Xna.Framework.Vector2").Resolve();
            MethodReference m_Vector2_get_Zero = MonoModRule.Modder.Module.ImportReference(t_Vector2.FindProperty("Zero").GetMethod);
            MethodReference m_Player_Die = MonoModRule.Modder.FindType("Celeste.Player").Resolve().FindMethod("Die");

            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdstr("Could not get out of solids when exiting Star Fly State!"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Call, m_Vector2_get_Zero);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Ldc_I4_1);
            cursor.Emit(OpCodes.Callvirt, m_Player_Die);
            cursor.Emit(OpCodes.Pop);
            cursor.RemoveRange(3);
        }

        public static void PatchPlayerOnCollideV(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_SurfaceIndex_GetPathFromIndex = context.Module.GetType("Celeste.SurfaceIndex").FindMethod("System.String GetPathFromIndex(System.Int32)");
            MethodReference m_String_Concat = MonoModRule.Modder.Module.ImportReference(
                MonoModRule.Modder.FindType("System.String").Resolve()
                    .FindMethod("System.String Concat(System.String,System.String)")
            );

            ILCursor cursor = new ILCursor(context);

            /*  Move cursor to after IL_040d in
                    // num2 = platformByPriority.GetLandSoundIndex(this)
                    IL_040a: ldloc.s 4
                    IL_040c: ldarg.0
                    IL_040d: callvirt instance int32 Celeste.Platform::GetLandSoundIndex(class Monocle.Entity)
                    IL_0412: stloc.s 5
                and get the variable index of num2  
            */
            int loc_landSoundIdx = -1;
            cursor.GotoNext(MoveType.After,
                instr => instr.MatchCallvirt("Celeste.Platform", "System.Int32 GetLandSoundIndex(Monocle.Entity)"),
                instr => instr.MatchStloc(out loc_landSoundIdx));

            /*  Change
                    Play((playFootstepOnLand > 0f) ? "event:/char/madeline/footstep" : "event:/char/madeline/landing", "surface_index", num2);
                to
                    Play(SurfaceIndex.GetPathFromIndex(num2) + ((playFootstepOnLand > 0f) ? "/footstep" : "/landing"), "surface_index", num2);
            */
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdarg(0), instr => instr.MatchLdfld("Celeste.Player", "playFootstepOnLand"));
            cursor.Emit(OpCodes.Ldloc, loc_landSoundIdx);
            cursor.Emit(OpCodes.Call, m_SurfaceIndex_GetPathFromIndex);
            cursor.GotoNext(instr => instr.MatchLdstr("event:/char/madeline/landing"))
                .Next.Operand = "/landing";
            cursor.GotoNext(instr => instr.MatchLdstr("event:/char/madeline/footstep"))
                .Next.Operand = "/footstep";
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdstr("surface_index"));
            cursor.Emit(OpCodes.Call, m_String_Concat);
        }

        public static void PatchPlayerClimbBegin(ILContext context, CustomAttribute attrib) {
            PatchPlaySurfaceIndex(new ILCursor(context), "/grab");
        }

        public static void PatchPlayerOrigWallJump(ILContext context, CustomAttribute attrib) {
            // Doesn't use PatchPlaySurfaceIndex because index, not platform, is stored in the local variable

            MethodDefinition m_SurfaceIndex_GetPathFromIndex = context.Module.GetType("Celeste.SurfaceIndex").FindMethod("System.String GetPathFromIndex(System.Int32)");
            MethodReference m_String_Concat = MonoModRule.Modder.Module.ImportReference(
                MonoModRule.Modder.FindType("System.String").Resolve()
                    .FindMethod("System.String Concat(System.String,System.String)")
            );

            ILCursor cursor = new ILCursor(context);

            /*  Change:
                    Play("event:/char/madeline/landing", "surface_index", num);
                to:
                    Play(SurfaceIndex.GetPathFromIndex(num) + "/landing", "surface_index", num);
            */
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdstr("event:/char/madeline/landing"));
            cursor.Emit(OpCodes.Ldloc_0);
            cursor.Emit(OpCodes.Call, m_SurfaceIndex_GetPathFromIndex);
            cursor.Emit(OpCodes.Ldstr, "/landing");
            cursor.Emit(OpCodes.Call, m_String_Concat);
            cursor.Remove();
        }

        public static void PatchPlayerCtorOnFrameChange(MethodDefinition method, CustomAttribute attrib) {
            method = method.DeclaringType.FindMethod("<.ctor>b__280_1");

            new ILContext(method).Invoke(il => {
                ILCursor cursor = new ILCursor(il);
                PatchPlaySurfaceIndex(cursor, "/footstep");
                PatchPlaySurfaceIndex(cursor, "/handhold");
            });
        }

    }
}
