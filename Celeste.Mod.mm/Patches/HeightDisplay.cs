using System;
using System.Collections;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    class patch_HeightDisplay : HeightDisplay {

        public patch_HeightDisplay(int index) : base(index) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchHeightDisplayRoutine]
        private extern IEnumerator Routine();

    }
}

namespace MonoMod {
    /// <summary>
    /// Patches HeightDisplay.Routine to fix the game crash when player dies.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchHeightDisplayRoutine))]
    class PatchHeightDisplayRoutineAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchHeightDisplayRoutine(MethodDefinition method, CustomAttribute attrib) {
            // while (approach < (float)height && !player.OnGround())
            // to
            // while (approach < (float)height && player.Scene != null && !player.OnGround())

            TypeDefinition t_Entity = MonoModRule.Modder.Module.GetType("Monocle.Entity");
            MethodDefinition m_GetScene = t_Entity.FindMethod("Monocle.Scene get_Scene()");

            // The routine is stored in a compiler-generated method.
            method = method.GetEnumeratorMoveNext();

            new ILContext(method).Invoke(il => {
                ILCursor cursor = new ILCursor(il);
                ILLabel outOfWhileLabel = null;
                cursor.GotoNext(MoveType.After, instr => instr.MatchBgeUn(out outOfWhileLabel));
                object playerOperand = cursor.Next.Next.Operand;
                cursor.Emit(OpCodes.Ldarg_0)
                    .Emit(OpCodes.Ldfld, playerOperand)
                    .Emit(OpCodes.Callvirt, m_GetScene)
                    .Emit(OpCodes.Brfalse_S, outOfWhileLabel);
            });
        }

    }
}
