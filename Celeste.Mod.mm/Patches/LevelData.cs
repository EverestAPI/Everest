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
            TypeDefinition DecalData = MonoModRule.Modder.FindType("Celeste.DecalData").Resolve();
            TypeDefinition BinaryPackerElement = MonoModRule.Modder.FindType("Celeste.BinaryPacker/Element").Resolve();

            FieldDefinition f_DecalDataRotation = DecalData.FindField("Rotation");
            MethodDefinition m_BinaryPackerElementAttrFloat = BinaryPackerElement.FindMethod("AttrFloat");

            ILCursor cursor = new ILCursor(context);

            int matches = 0;
            while (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallvirt("System.Collections.Generic.List`1<Celeste.DecalData>", "Add"))) {
                /*
                we are inserting:

                // decaldata.Texture = (string)element.Attributes["texture"];
                   IL_0a5f: dup
                   IL_0a60: ldloc.s 11
                   IL_0a62: ldfld class [mscorlib]System.Collections.Generic.Dictionary`2<string, object> Celeste.BinaryPacker/Element::Attributes
                   IL_0a67: ldstr "texture"
                   IL_0a6c: callvirt instance !1 class [mscorlib]System.Collections.Generic.Dictionary`2<string, object>::get_Item(!0)
                   IL_0a71: castclass [mscorlib]System.String
                   IL_0a76: stfld string Celeste.DecalData::Texture
                // decaldata.Rotation = element.AttrFloat("rotation", 0.0f);
                        ->  dup
                            ldloc.s 11
                            ldstr "rotation"
                            ldc_r4 0.0
                            callvirt instance float32 Celeste.BinaryPacker/Element::AttrFloat(string, float32)
                            stfld float32 Celeste.DecalData::Rotation
                // BgDecals.Add(decaldata); // or FgDecals
                   IL_0a7b: callvirt instance void class [mscorlib]System.Collections.Generic.List`1<class Celeste.DecalData>::Add(!0)

                in both places where a DecalData instance is added to a List
                */

                // first we find the binarypacker element representing the decal:
                cursor.FindPrev(out ILCursor[] packer_element_loc_cursors, instr => instr.MatchLdloc(out int _));
                packer_element_loc_cursors[0].Next.MatchLdloc(out int packer_element_loc);

                // now, we duplicate the DecalData reference
                cursor.Emit(OpCodes.Dup);
                // ask for the rotation field from the packer element, or a default of 0.0f
                cursor.Emit(OpCodes.Ldloc, packer_element_loc);
                cursor.Emit(OpCodes.Ldstr, "rotation");
                cursor.Emit(OpCodes.Ldc_R4, (float)Math.PI / 2f);
                cursor.Emit(OpCodes.Callvirt, m_BinaryPackerElementAttrFloat);
                // store the rotation in the decaldata
                cursor.Emit(OpCodes.Stfld, f_DecalDataRotation);

                cursor.Index++;
                matches++;
            }
            if (matches != 2) {
                throw new Exception($"Too few matches for HasAttr(\"tag\"): {matches}");
            }
        }
    }
}
