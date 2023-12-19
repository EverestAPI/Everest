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

        private bool _IsShallowAtRectangle(Rectangle rectangle) {
            return Scene.CollideCheck<Solid>(new Vector2(rectangle.Left, Top + 8), new Vector2(rectangle.Right, Top + 8));
        }

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
            TypeDefinition t_WaterInteraction = MonoModRule.Modder.Module.GetType("Celeste.WaterInteraction");
            MethodReference m_WaterInteraction_Check = t_WaterInteraction.FindMethod("Check")!;
            MethodReference m_WaterInteraction_get_Bounds = t_WaterInteraction.FindProperty("Bounds")!.GetMethod;
            MethodReference m_WaterInteraction_get_AbsoluteCenter = t_WaterInteraction.FindProperty("AbsoluteCenter")!.GetMethod;

            TypeDefinition t_Water = context.Method.DeclaringType;
            MethodReference m_Water_IsShallowAtRectangle = t_Water.FindMethod("_IsShallowAtRectangle")!;
            
            TypeReference t_Vector2 = m_WaterInteraction_get_AbsoluteCenter.ReturnType;
            VariableDefinition v_AbsoluteCenter = new VariableDefinition(t_Vector2);
            context.Body.Variables.Add(v_AbsoluteCenter);
            
            ILCursor cursor = new ILCursor(context);
            
            // store the WaterInteraction's AbsoluteCenter in a local variable
            int waterInteractionVar = -1;
            cursor.GotoNext(MoveType.After, instr => instr.MatchCastclass("Celeste.WaterInteraction"), instr => instr.MatchStloc(out waterInteractionVar));
            cursor.EmitLdloc(waterInteractionVar);
            cursor.EmitCall(m_WaterInteraction_get_AbsoluteCenter);
            cursor.EmitStloc(v_AbsoluteCenter);
            
            // replace: this.CollideCheck(entity)
            // with: component.Check(this)
            int parentEntityVar = -1;
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdarg0(), instr => instr.MatchLdloc(out parentEntityVar), instr => instr.MatchCall("Monocle.Entity", "CollideCheck"));
            cursor.EmitLdloc(waterInteractionVar);
            cursor.EmitLdarg0();
            cursor.EmitCall(m_WaterInteraction_Check);
            cursor.RemoveRange(3);
            
            // Replace the Rectangle creation, and any values used for it, with our custom shallowness check.
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdarg0(), instr => instr.MatchCall("Monocle.Entity", "get_Scene"));
            cursor.EmitLdarg0();
            cursor.EmitLdloc(waterInteractionVar);
            cursor.EmitCall(m_WaterInteraction_get_Bounds);
            cursor.EmitCall(m_Water_IsShallowAtRectangle);
            while (!cursor.Next!.MatchBrfalse(out _)) {
                cursor.Remove();
            }

            // replace all instances of entity.Center with the stored AbsoluteCenter
            cursor.Goto(0);
            int matches = 0;
            while (cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchLdloc(parentEntityVar), instr => instr.MatchCallvirt("Monocle.Entity", "get_Center"))) {
                matches++;
                cursor.EmitLdloc(v_AbsoluteCenter);
                cursor.RemoveRange(2);
            }
            if (matches != 9) {
                throw new Exception($"Incorrect number of matches for entity.Center: {matches}");
            }
        }

    }
}
