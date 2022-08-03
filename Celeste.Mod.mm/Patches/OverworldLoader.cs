#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using Celeste.Mod.Core;
using MonoMod;
using System.Collections;
using System.Threading;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_OverworldLoader : OverworldLoader {

        public patch_OverworldLoader(Overworld.StartMode startMode, HiresSnow snow = null)
            : base(startMode, snow) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore] // don't change anything in the method...
        [PatchTotalHeartGemChecks] // except for replacing TotalHeartGems with TotalHeartGemsInVanilla through MonoModRules
        private extern void CheckVariantsPostcardAtLaunch();

        [MonoModIgnore] // don't change anything in the method...
        [PatchTotalHeartGemCSidePostcard] // except for replacing TotalHeartGems with TotalHeartGemsInVanilla through MonoModRules
        private extern IEnumerator Routine(Session session);
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch TotalHeartGems to refer to TotalHeartGemsInVanilla, and whether to show the UnlockCSide postcard
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchTotalHeartGemCSidePostcard))]
    class PatchTotalHeartGemCSidePostcardAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchTotalHeartGemCSidePostcard(MethodDefinition method, CustomAttribute attrib) {
            FieldDefinition f_SaveData_Instance = method.Module.GetType("Celeste.SaveData").FindField("Instance");
            MethodDefinition m_SaveData_get_LevelSetStats = method.Module.GetType("Celeste.SaveData").FindMethod("Celeste.LevelSetStats get_LevelSetStats()");
            MethodDefinition m_LevelSetStats_get_MaxAreaMode = method.Module.GetType("Celeste.LevelSetStats").FindMethod("System.Int32 get_MaxAreaMode()");

            // Routines are stored in compiler-generated methods.
            method = method.GetEnumeratorMoveNext();

            new ILContext(method).Invoke(il => {
                ILCursor cursor = new ILCursor(il);

                cursor.GotoNext(MoveType.After, instr => instr.MatchLdfld("Celeste.Session", "UnlockedCSide"));
                cursor.Emit(cursor.Next.OpCode, cursor.Next.Operand);
                cursor.Emit(OpCodes.Ldsfld, f_SaveData_Instance);
                cursor.Emit(OpCodes.Callvirt, m_SaveData_get_LevelSetStats);
                cursor.Emit(OpCodes.Callvirt, m_LevelSetStats_get_MaxAreaMode);
                cursor.Emit(OpCodes.Ldc_I4_2);
                cursor.Next.OpCode = OpCodes.Blt_S;

                PatchTotalHeartGemChecks(il, attrib);
            });
        }

    }
}
