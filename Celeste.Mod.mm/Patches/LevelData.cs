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
        [PatchLevelDataDecalLoader]
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

    /// <summary>
    /// A patch for the decal loading, allowing for rotated decals.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLevelDataDecalLoader))]
    class PatchLevelDataDecalLoader : Attribute { }

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


        public static void PatchLevelDataDecalLoader(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_DecalData = MonoModRule.Modder.FindType("Celeste.DecalData").Resolve();
            TypeDefinition t_BinaryPackerElement = MonoModRule.Modder.FindType("Celeste.BinaryPacker/Element").Resolve();

            FieldDefinition f_DecalDataRotation = t_DecalData.FindField("Rotation");
            MethodDefinition m_BinaryPackerElementAttrFloat = t_BinaryPackerElement.FindMethod("AttrFloat");

            // Goal is to set: decaldata.Rotation = element.AttrFloat("rotation")
            ILCursor cursor = new ILCursor(context);

            int local = -1;
            int matches = 0;
            // Grab the local variable holding the BinaryPacker Element and move to just before the DecalData is finished
            while (cursor.TryGotoNext(instr => instr.MatchLdloc(out local), instr => instr.OpCode == OpCodes.Ldfld, instr => instr.MatchLdstr("texture"))) {
                cursor.GotoNext(MoveType.After, instr => instr.MatchStfld("Celeste.DecalData", "Texture"));
                // Duplicate the DecalData reference so we can add one more field
                cursor.Emit(OpCodes.Dup);
                // Load in the rotation attribute from the BinaryPacker Element and set the DecalData field
                cursor.Emit(OpCodes.Ldloc, local);
                cursor.Emit(OpCodes.Ldstr, "rotation");
                cursor.Emit(OpCodes.Ldc_R4, 0.0f);
                cursor.Emit(OpCodes.Callvirt, m_BinaryPackerElementAttrFloat);
                cursor.Emit(OpCodes.Stfld, f_DecalDataRotation);
                matches++;
            }
            // We need to run this patch on FG and BG decals, so look for two matches
            if (matches != 2) {
                throw new Exception($"Too few matches for HasAttr(\"tag\"): {matches}");
            }
        }
    }
}
