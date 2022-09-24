#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System.Globalization;

namespace Celeste {
    public class patch_LevelData : LevelData {

        public Vector2? DefaultSpawn;

        public patch_LevelData(BinaryPacker.Element data) : base(data) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchLevelDataBerryTracker]
        [PatchLevelDataDecalLoader]
        [PatchLevelDataSpawnpointLoader]
        public extern void orig_ctor(BinaryPacker.Element data);

        [MonoModConstructor]
        public void ctor(BinaryPacker.Element data) {
            orig_ctor(data);
        }

        private void CheckForDefaultSpawn(BinaryPacker.Element spawn, Vector2 coords) {
            if (DefaultSpawn == null && spawn.Attributes.TryGetValue("isDefaultSpawn", out object isDefaultSpawn) && Convert.ToBoolean(isDefaultSpawn, CultureInfo.InvariantCulture)) {
                DefaultSpawn = coords;
            }
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

    /// <summary>
    /// Patch the constructor to recognize the default spawnpoint.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLevelDataSpawnpointLoader))]
    class PatchLevelDataSpawnpointLoaderAttribute : Attribute { }

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

            ILCursor cursor = new ILCursor(context);

            int loc_element = -1;
            int matches = 0;
            // for each of the two places DecalData instances are created (one for FGDecals and one for BGDecals), move to just after the texture is stored;
            // also obtain a reference to the BinaryPacker.Element that holds the decal's map data
            while (cursor.TryGotoNext(instr => instr.MatchLdloc(out loc_element),
                                      instr => instr.MatchLdfld(out FieldReference _),
                                      instr => instr.MatchLdstr("texture"))) {
                cursor.GotoNext(MoveType.After, instr => instr.MatchStfld("Celeste.DecalData", "Texture"));

                // we are trying to add:
                //   decaldata.Rotation = element.AttrFloat("rotation", 0.0f);

                // copy the reference to the DecalData
                cursor.Emit(OpCodes.Dup);
                // load the rotation from the BinaryPacker.Element, with a default of 0.0f
                cursor.Emit(OpCodes.Ldloc, loc_element);
                cursor.Emit(OpCodes.Ldstr, "rotation");
                cursor.Emit(OpCodes.Ldc_R4, 0.0f);
                cursor.Emit(OpCodes.Callvirt, m_BinaryPackerElementAttrFloat);
                // put the rotation into the DecalData
                cursor.Emit(OpCodes.Stfld, f_DecalDataRotation);

                matches++;
            }
            if (matches != 2) {
                throw new Exception($"Too few matches for HasAttr(\"tag\"): {matches}");
            }
        }

        public static void PatchLevelDataSpawnpointLoader(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_LevelDataCheckForDefaultSpawn = context.Method.DeclaringType.FindMethod("CheckForDefaultSpawn");
            TypeReference t_Vector2 = MonoModRule.Modder.FindType("Microsoft.Xna.Framework.Vector2").Resolve();
            t_Vector2 = MonoModRule.Modder.Module.ImportReference(t_Vector2);
            VariableDefinition v_spawnCoords = new VariableDefinition(t_Vector2);
            context.Body.Variables.Add(v_spawnCoords);

            ILCursor cursor = new ILCursor(context);

            int element = -1;
            // call CheckForDefaultSpawn with checkpoint element and coordinates
            cursor.GotoNext(instr => instr.MatchLdloc(out element), instr => instr.MatchLdfld("Celeste.BinaryPacker/Element", "Name"), instr => instr.MatchLdstr("player"));
            cursor.GotoNext(MoveType.After, instr => instr.MatchNewobj("Microsoft.Xna.Framework.Vector2"));
            cursor.Emit(OpCodes.Stloc, v_spawnCoords);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc, element);
            cursor.Emit(OpCodes.Ldloc, v_spawnCoords);
            cursor.Emit(OpCodes.Callvirt, m_LevelDataCheckForDefaultSpawn);
            cursor.Emit(OpCodes.Ldloc, v_spawnCoords);
        }
    }
}
