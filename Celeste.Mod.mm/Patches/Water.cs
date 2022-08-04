using System;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    class patch_Water : Water {

        public patch_Water(EntityData data, Vector2 offset) : base(data, offset) {
        }

        [MonoModIgnore] // we don't want to change anything in the method...
        [PatchWaterUpdate] // ... except manipulating it manually with MonoModRules
        public extern override void Update();

    }
}

namespace MonoMod {
    /// <summary>
    /// Modify collision to make it customizable.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchWaterUpdate))]
    class PatchWaterUpdateAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchWaterUpdate(ILContext context, CustomAttribute attrib) {
            MethodReference m_WaterInteraction_get_Bounds = MonoModRule.Modder.Module.GetType("Celeste.WaterInteraction").FindProperty("Bounds").GetMethod;
            TypeReference t_Rectangle = m_WaterInteraction_get_Bounds.ReturnType;
            MethodReference m_Rectangle_get_Center = MonoModRule.Modder.Module.ImportReference(t_Rectangle.Resolve().FindProperty("Center").GetMethod);
            TypeReference t_Point = m_Rectangle_get_Center.ReturnType;
            FieldReference f_Point_Y = MonoModRule.Modder.Module.ImportReference(t_Point.Resolve().FindField("Y"));

            MethodReference m_Component_get_Entity = MonoModRule.Modder.Module.GetType("Monocle.Component").FindMethod("Monocle.Entity get_Entity()");
            MethodReference m_Entity_CollideRect = MonoModRule.Modder.Module.GetType("Monocle.Entity").FindMethod($"System.Boolean CollideRect({t_Rectangle.FullName})");

            MethodReference m_Point_ToVector2 = MonoModRule.Modder.Module.GetType("Celeste.Mod.Extensions").FindMethod($"Microsoft.Xna.Framework.Vector2 Celeste.Mod.Extensions::ToVector2(Microsoft.Xna.Framework.Point)");

            VariableDefinition v_Bounds = new VariableDefinition(t_Rectangle);
            context.Body.Variables.Add(v_Bounds);

            ILCursor cursor = new ILCursor(context);
            // Load the WaterInteraction Bounds into a local variable
            cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt("Monocle.Component", "get_Entity"));
            cursor.Prev.Operand = m_WaterInteraction_get_Bounds;
            cursor.Next.OpCode = OpCodes.Stloc_S;
            cursor.Next.Operand = v_Bounds;

            // Replace the collision check (I think this technically loses precision but nobody's complained yet)
            cursor.GotoNext(instr => instr.MatchCall("Monocle.Entity", "CollideCheck"));
            cursor.Next.Operand = m_Entity_CollideRect;

            // Replace the Rectangle creation, and any values used for it, with our Bounds value.
            cursor.GotoNext(MoveType.After, instr => instr.MatchCall("Monocle.Entity", "get_Scene"));
            while (!cursor.Next.MatchNewobj("Microsoft.Xna.Framework.Rectangle")) {
                cursor.Remove();
            }
            cursor.Next.OpCode = OpCodes.Ldloc_S;
            cursor.Next.Operand = v_Bounds;

            // Start again from the top and retrieve the Bounds instead of the entity (but only up to a certain point)
            cursor.Goto(0);
            for (int i = 0; i < 10; i++) {
                cursor.GotoNext(MoveType.After, instr => instr.OpCode == OpCodes.Ldloc_3);
                cursor.Prev.OpCode = OpCodes.Ldloca_S;
                cursor.Prev.Operand = v_Bounds;

                // Modify any method calls/field accesses to the Bounds
                if (cursor.Next.MatchCallvirt("Monocle.Entity", "get_Center")) {
                    cursor.Remove();
                    cursor.Emit(OpCodes.Call, m_Rectangle_get_Center);
                    if (cursor.Next.OpCode == OpCodes.Ldfld) {
                        cursor.Remove();
                        cursor.Emit(OpCodes.Ldfld, f_Point_Y);
                        cursor.Emit(OpCodes.Conv_R4);
                    } else {
                        cursor.Emit(OpCodes.Call, m_Point_ToVector2);
                    }
                } else {
                    cursor.Prev.OpCode = OpCodes.Ldloc_S;
                    cursor.Prev.Operand = v_Bounds;
                }
            }

            // We have reached the end of the code to be patched, we can finally load the WaterInteraction's Entity and continue as normal
            cursor.GotoNext(instr => instr.Next.MatchIsinst("Celeste.Player"));
            cursor.Emit(OpCodes.Ldloc_2);
            cursor.Emit(OpCodes.Callvirt, m_Component_get_Entity);
            cursor.Emit(OpCodes.Stloc_3);
        }

    }
}
