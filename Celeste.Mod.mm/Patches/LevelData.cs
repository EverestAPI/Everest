#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System.Collections.Generic;
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

        // Optimise the method
        [MonoModReplace]
        private EntityData CreateEntityData(BinaryPacker.Element entity) {
            EntityData entityData = new() {
                Name = entity.Name,
                Level = this
            };
            
            if (entity.Attributes != null) {
                foreach ((string key, object value) in entity.Attributes) {
                    switch (key)
                    {
                        case "id":
                            entityData.ID = (int) value;
                            break;
                        case "x":
                            entityData.Position.X = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                            break;
                        case "y":
                            entityData.Position.Y = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                            break;
                        case "width":
                            entityData.Width = (int) value;
                            break;
                        case "height":
                            entityData.Height = (int) value;
                            break;
                        case "originX":
                            entityData.Origin.X = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                            break;
                        case "originY":
                            entityData.Origin.Y = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                            break;
                        default:
                        {
                            // We'll assume that most entities have id, x, y but not width, height, originX/Y
                            // This means our resulting dict should have count - 3 elements in the end.
                            // For resizable entities this makes the dict too large,
                            // but auto-resizing from passing a capacity too small would probably make it too big anyway.
                            entityData.Values ??= new Dictionary<string, object>(entity.Attributes.Count - 3);
                            
                            entityData.Values.Add(key, value);
                            break;
                        }
                    }
                }
            }

            if (entity.Children is { Count: > 0 }) {
                entityData.Nodes = new Vector2[entity.Children.Count];
                for (int index = 0; index < entityData.Nodes.Length; index++) {
                    var attributesFromBinary = entity.Children[index].Attributes;
                    ref var node = ref entityData.Nodes[index];

                    if (attributesFromBinary.TryGetValue("x", out object x))
                        node.X = Convert.ToSingle(x, CultureInfo.InvariantCulture);
                    if (attributesFromBinary.TryGetValue("y", out object y))
                        node.Y = Convert.ToSingle(y, CultureInfo.InvariantCulture);
                }
            } else {
                entityData.Nodes = Array.Empty<Vector2>();
            }

            return entityData;
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

            bool found = false;

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
                    found = true;
                }
            }

            if (!found) {
                throw new Exception("No \"strawberry\" string found in " + method.FullName + "!");
            }
        }


        public static void PatchLevelDataDecalLoader(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_DecalData = MonoModRule.Modder.FindType("Celeste.DecalData").Resolve();
            TypeDefinition t_BinaryPackerElement = MonoModRule.Modder.FindType("Celeste.BinaryPacker/Element").Resolve();
            TypeDefinition t_Extensions = MonoModRule.Modder.FindType("Celeste.Mod.Extensions").Resolve();

            MethodDefinition m_BinaryPackerElementHasAttr         = t_BinaryPackerElement.FindMethod("HasAttr");
            MethodDefinition m_BinaryPackerElementAttr            = t_BinaryPackerElement.FindMethod("Attr");
            MethodDefinition m_BinaryPackerElementAttrFloat       = t_BinaryPackerElement.FindMethod("AttrFloat");
            MethodDefinition m_BinaryPackerElementAttrNullableInt = t_Extensions.FindMethod("AttrNullableInt");

            FieldDefinition f_DecalDataRotation = t_DecalData.FindField("Rotation");
            FieldDefinition f_DecalDataColorHex = t_DecalData.FindField("ColorHex");
            FieldDefinition f_DecalDataDepth    = t_DecalData.FindField("Depth");

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
                //   decaldata.ColorHex = element.AttrString("color", "");
                //   if (element.HasAttr("depth"))
                //      decaldata.Depth = element.AttrNullableInt("depth");

                // copy the reference to the DecalData
                cursor.Emit(OpCodes.Dup);
                // load the rotation from the BinaryPacker.Element, with a default of 0.0f
                cursor.Emit(OpCodes.Ldloc, loc_element);
                cursor.Emit(OpCodes.Ldstr, "rotation");
                cursor.Emit(OpCodes.Ldc_R4, 0.0f);
                cursor.Emit(OpCodes.Call, m_BinaryPackerElementAttrFloat);
                // put the rotation into the DecalData
                cursor.Emit(OpCodes.Stfld, f_DecalDataRotation);

                // copy the reference to the DecalData again
                cursor.Emit(OpCodes.Dup);
                // load the hex color from the BinaryPacker.Element
                cursor.Emit(OpCodes.Ldloc, loc_element);
                cursor.Emit(OpCodes.Ldstr, "color");
                cursor.Emit(OpCodes.Ldstr, "");
                cursor.Emit(OpCodes.Call, m_BinaryPackerElementAttr);
                // put the color into the DecalData
                cursor.Emit(OpCodes.Stfld, f_DecalDataColorHex);

                // find out if there is a depth field in the BinaryPacker.Element
                cursor.Emit(OpCodes.Ldloc, loc_element);
                cursor.Emit(OpCodes.Ldstr, "depth");
                cursor.Emit(OpCodes.Call, m_BinaryPackerElementHasAttr);
                // if not, skip to after setting it
                ILLabel after_attr_depth = cursor.DefineLabel();
                cursor.Emit(OpCodes.Brfalse_S, after_attr_depth);

                // copy the reference to the DecalData again
                cursor.Emit(OpCodes.Dup);
                // load the depth from the BinaryPacker.Element
                cursor.Emit(OpCodes.Ldloc, loc_element);
                cursor.Emit(OpCodes.Ldstr, "depth");
                cursor.Emit(OpCodes.Call, m_BinaryPackerElementAttrNullableInt);
                // put the depth into the DecalData
                cursor.Emit(OpCodes.Stfld, f_DecalDataDepth);

                cursor.MarkLabel(after_attr_depth);

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
