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
using System.Linq;


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
        [PatchCSidePostcardText] // and checking for a custom C-side unlock postcard through MonoModRules
        private extern IEnumerator Routine(Session session);


        /// <summary>
        /// A helper function that is called from <c>Routine</c> to determent which c-side unlock postcard to display
        /// </summary>
        private static string GetCSidePostcard(Session session) {
            patch_AreaData areaData = patch_AreaData.Get(session);
            string customLevelCSidePostcardDialog = $"{areaData.Name}_CSIDES_POSTCARD";

            if (areaData.LevelSet == "Celeste" || !Dialog.Has(customLevelCSidePostcardDialog)) {
                return "POSTCARD_CSIDES";
            } else {
                return customLevelCSidePostcardDialog;
            }
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch TotalHeartGems to refer to TotalHeartGemsInVanilla, and whether to show the UnlockCSide postcard
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchTotalHeartGemCSidePostcard))]
    class PatchTotalHeartGemCSidePostcardAttribute : Attribute { }

    /// <summary>
    /// Patch Routine to get the dialog for the C-side unlock instead of the constant "POSTCARD_CSIDES"
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchCSidePostcardText))]
    class PatchCSidePostcardTextAttribute : Attribute { }

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

        public static void PatchCSidePostcardText(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_OverworldLoader_GetCSidePostcard = method.Module.GetType("Celeste.OverworldLoader").FindMethod("System.String GetCSidePostcard(Celeste.Session)");

            method = method.GetEnumeratorMoveNext();

            FieldDefinition f_session = method.DeclaringType.Fields.FirstOrDefault(f => f.FieldType.Name == "Session");

            new ILContext(method).Invoke(il => {
                ILCursor cursor = new ILCursor(il);

                cursor.GotoNext(MoveType.Before, instr => instr.MatchLdstr("POSTCARD_CSIDES"));
                cursor.Remove();
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, f_session);
                cursor.Emit(OpCodes.Call, m_OverworldLoader_GetCSidePostcard);
            });
        }
    }
}
