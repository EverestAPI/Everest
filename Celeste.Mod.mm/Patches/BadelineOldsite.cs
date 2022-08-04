#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0414 // The field is assigned but its value is never used

using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_BadelineOldsite : BadelineOldsite {

        private bool following;
        private bool ignorePlayerAnim;

        private bool canChangeMusic;

        public patch_BadelineOldsite(Vector2 position, int index)
            : base(position, index) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(EntityData data, Vector2 offset, int index);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset, int index) {
            orig_ctor(data, offset, index);

            canChangeMusic = data.Bool("canChangeMusic", true);
        }

        // We're hooking the original Added, thus can't call base (Monocle.Entity::Added) without a small workaround.
        [MonoModLinkTo("Monocle.Entity", "Added")]
        [MonoModForceCall]
        [MonoModRemove]
        public extern void base_Added(Scene scene);
        public extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            Level level = scene as Level;
            if (level?.Session.Area.GetLevelSet() == "Celeste") {
                orig_Added(scene);
                return;
            }

            base_Added(scene);
            Add(new Coroutine(StartChasingRoutine(level)));
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchBadelineChaseRoutine] // ... except for manually manipulating the method via MonoModRules
        public new extern IEnumerator StartChasingRoutine(Level level);

        private extern IEnumerator orig_StopChasing();
        private IEnumerator StopChasing() {
            Level level = Scene as Level;
            if (level.Session.Area.GetLevelSet() == "Celeste")
                return orig_StopChasing();

            return custom_StopChasing();
        }
        private IEnumerator custom_StopChasing() {
            Level level = Scene as Level;

            while (!CollideCheck<BadelineOldsiteEnd>())
                yield return null;

            following = false;
            ignorePlayerAnim = true;
            Sprite.Play("laugh");
            Sprite.Scale.X = 1f;
            yield return 1f;

            Audio.Play("event:/char/badeline/disappear", Position);
            level.Displacement.AddBurst(Center, 0.5f, 24f, 96f, 0.4f);
            level.Particles.Emit(P_Vanish, 12, Center, Vector2.One * 6f);
            RemoveSelf();
        }

        public bool IsChaseEnd(bool value) {
            Level level = Scene as Level;
            if (level.Session.Area.GetLevelSet() == "Celeste")
                return value;

            if (level.Tracker.CountEntities<BadelineOldsiteEnd>() != 0)
                return true;

            return false;
        }

        public bool CanChangeMusic(bool value) {
            Level level = Scene as Level;
            if (level.Session.Area.GetLevelSet() == "Celeste")
                return value;

            return canChangeMusic;
        }

    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the Badeline chase routine instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchBadelineChaseRoutine))]
    class PatchBadelineChaseRoutineAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchBadelineChaseRoutine(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_CanChangeMusic = method.DeclaringType.FindMethod("System.Boolean Celeste.BadelineOldsite::CanChangeMusic(System.Boolean)");
            MethodDefinition m_IsChaseEnd = method.DeclaringType.FindMethod("System.Boolean Celeste.BadelineOldsite::IsChaseEnd(System.Boolean)");

            // The routine is stored in a compiler-generated method.
            method = method.GetEnumeratorMoveNext();
            FieldDefinition f_this = method.DeclaringType.FindField("<>4__this");

            new ILContext(method).Invoke(il => {
                ILCursor cursor = new ILCursor(il);

                // Add this.CanChangeMusic()
                cursor.GotoNext(instr => instr.OpCode == OpCodes.Ldarg_0,
                    instr => instr.MatchLdfld(out FieldReference f) && f.Name == "level");
                // Push this and grab this from this.
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, f_this);

                cursor.GotoNext(MoveType.After, instr => instr.MatchLdfld("Celeste.AreaKey", "Mode"));
                // Insert `== 0`
                cursor.Emit(OpCodes.Ldc_I4_0);
                cursor.Emit(OpCodes.Ceq);
                // Replace brtrue with brfalse
                cursor.Next.OpCode = OpCodes.Brfalse_S;

                // Process.
                cursor.Emit(OpCodes.Call, m_CanChangeMusic);

                // Add this.IsChaseEnd()
                cursor.GotoNext(instr => instr.OpCode == OpCodes.Ldarg_0,
                    instr => instr.MatchLdfld(out FieldReference f) && f.Name == "level",
                    instr => true, instr => true, instr => instr.MatchLdstr("2"));
                // Push this and grab this from this.
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, f_this);

                cursor.GotoNext(MoveType.After, instr => instr.MatchLdstr("2"),
                    instr => instr.MatchCall<string>("op_Equality"));
                // Process.
                cursor.Emit(OpCodes.Call, m_IsChaseEnd);
            });
        }

    }
}
