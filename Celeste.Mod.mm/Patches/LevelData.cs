#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    public class patch_LevelData : LevelData {

        public patch_LevelData(BinaryPacker.Element data) : base(data) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchLevelDataBerryTracker]
        public extern void orig_ctor(BinaryPacker.Element data);

        [MonoModConstructor]
        public void ctor(BinaryPacker.Element data) {
            orig_ctor(data);
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// A patch for the strawberry tracker, allowing all registered modded berries to be detected.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLevelDataBerryTracker))]
    class PatchLevelDataBerryTracker : Attribute { }

    static partial class MonoModRules {

        public static void PatchLevelDataBerryTracker(MethodDefinition method, CustomAttribute attrib) {
            TypeDefinition t_StrawberryRegistry = MonoModRule.Modder.FindType("Celeste.Mod.StrawberryRegistry")?.Resolve();
            MethodDefinition m_TrackableContains = t_StrawberryRegistry.FindMethod("System.Boolean TrackableContains(Celeste.BinaryPacker/Element)");

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                /*
                   we found

                   IL_08BA: ldloc.s   V_14
                   IL_08BC: ldfld     string Celeste.BinaryPacker/Element::Name
                   IL_08C1: ldstr     "strawberry"      <-- YOU ARE HERE
                   IL_08C6: call      bool [mscorlib]System.String::op_Equality(string, string)
                   IL_08CB: brtrue.s  IL_08E0
                */

                // Strawberry tracker adjustments
                if (instr.MatchLdstr("strawberry")) {
                    instr.OpCode = OpCodes.Nop;
                    instrs[instri - 1].OpCode = OpCodes.Nop;
                    instrs[instri + 1].Operand = m_TrackableContains;
                    instri++;
                }
            }
        }

    }
}
