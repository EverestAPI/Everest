#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using System;
using Monocle;
using MonoMod;
using System.Collections;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    class patch_AscendManager {
        [MonoModIgnore]
        [PatchAscendManagerRoutine]
        private extern IEnumerator Routine();

        private bool ShouldRestorePlayerX() {
            return (Engine.Scene as Level).Session.Area.GetLevelSet() != "Celeste";
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches AscendManager.Routine to fix gameplay RNG in custom maps.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchAscendManagerRoutine))]
    class PatchAscendManagerRoutineAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchAscendManagerRoutine(MethodDefinition method, CustomAttribute attrib) {
            // The routine is stored in a compiler-generated method.
            MethodDefinition routine = method.GetEnumeratorMoveNext();

            TypeDefinition t_Vector2 = MonoModRule.Modder.FindType("Microsoft.Xna.Framework.Vector2").Resolve();

            MethodDefinition m_ShouldRestorePlayerX = method.DeclaringType.FindMethod("System.Boolean ShouldRestorePlayerX()");
            MethodDefinition m_Entity_set_X = method.Module.GetType("Monocle.Entity").FindMethod("System.Void set_X(System.Single)").Resolve();

            FieldDefinition f_this = routine.DeclaringType.FindField("<>4__this");
            FieldDefinition f_player = routine.DeclaringType.Fields.FirstOrDefault(f => f.Name.StartsWith("<player>5__"));
            FieldDefinition f_from = routine.DeclaringType.Fields.FirstOrDefault(f => f.Name.StartsWith("<from>5__"));
            FieldReference f_Vector2_X = MonoModRule.Modder.Module.ImportReference(t_Vector2.FindField("X"));

            new ILContext(routine).Invoke(il => {
                ILCursor cursor = new ILCursor(il);

                // move after this.Scene.Add(fader)
                cursor.GotoNext(MoveType.After,
                    instr => instr.MatchLdarg(0),
                    instr => instr.OpCode == OpCodes.Ldfld && ((FieldReference) instr.Operand).Name.StartsWith("<fader>5__"),
                    instr => instr.OpCode == OpCodes.Callvirt && ((MethodReference) instr.Operand).GetID() == "System.Void Monocle.Scene::Add(Monocle.Entity)");

                // target: from = player.Position;
                Instruction target = cursor.Next;

                // _ = this.ShouldRestorePlayerX();
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, f_this);
                cursor.Emit(OpCodes.Call, m_ShouldRestorePlayerX);

                // if (!_) goto target;
                cursor.Emit(OpCodes.Brfalse, target);

                // player.X = from.X;
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, f_player);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldflda, f_from);
                cursor.Emit(OpCodes.Ldfld, f_Vector2_X);
                cursor.Emit(OpCodes.Callvirt, m_Entity_set_X);
            });
        }

    }
}
