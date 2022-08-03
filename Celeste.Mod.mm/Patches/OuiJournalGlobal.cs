using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_OuiJournalGlobal : OuiJournalGlobal {
        public patch_OuiJournalGlobal(OuiJournal journal)
            : base(journal) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModConstructor]
        [MonoModIgnore] // don't change anything in the method...
        [PatchOuiJournalStatsHeartGemCheck] // except for replacing TotalHeartGems with TotalHeartGemsInVanilla through MonoModRules
        public extern void ctor(OuiJournal journal);

    }
}

namespace MonoMod {
    /// <summary>
    /// Patch a reference to TotalHeartGems in the OuiJournalGlobal constructor to unharcode the check for golden berry unlock.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchOuiJournalStatsHeartGemCheck))]
    class PatchOuiJournalStatsHeartGemCheckAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchOuiJournalStatsHeartGemCheck(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_getUnlockedModes = context.Method.Module.GetType("Celeste.SaveData").FindMethod("System.Int32 get_UnlockedModes()");

            ILCursor cursor = new ILCursor(context);

            /*
            We want to replace `SaveData.Instance.TotalHeartGems >= 16` with `SaveData.Instance.UnlockedModes >= 3`.
            This way, we only display the golden berry stat when golden berries are actually unlocked in the level set we are in.
            (UnlockedModes returns 3 if and only if TotalHeartGems is more than 16 in the vanilla level set anyway.)
            */

            // Move between these two instructions
            cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt("Celeste.SaveData", "get_TotalHeartGems") &&
                instr.Next.MatchLdcI4(16));
            cursor.Prev.Operand = m_getUnlockedModes;
            cursor.Next.OpCode = OpCodes.Ldc_I4_3;
        }

    }
}
