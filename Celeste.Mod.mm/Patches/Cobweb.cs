#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Linq;

namespace Celeste {
    class patch_Cobweb : Cobweb {

        public Color[] OverrideColors;

        public patch_Cobweb(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            AreaData area = AreaData.Get(scene);

            Color[] prevColors = area.CobwebColor;
            if (OverrideColors != null)
                area.CobwebColor = OverrideColors;

            orig_Added(scene);

            area.CobwebColor = prevColors;
        }

        [MonoModIgnore]
        [PatchCobwebDrawCobweb]
        private extern void DrawCobweb(Vector2 a, Vector2 b, int steps, bool drawOffshoots);

        private static bool IsVisible(SimpleCurve curve) {
            return CullHelper.IsCurveVisible(curve);
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the method to implement culling.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchDrawCobweb))]
    class PatchCobwebDrawCobweb : Attribute { }

    static partial class MonoModRules {
        public static void PatchDrawCobweb(ILContext il, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(il);

            VariableDefinition curveLocal = cursor.Body.Variables.First(v => v.VariableType.Name.Contains("SimpleCurve"));

            // inject our culling after the recursive DrawCobweb call - we need to cull each offshoot individually or we'll have pop-in.
            cursor.GotoNext(MoveType.After, instr => instr.MatchLdfld("Monocle.SimpleCurve", "Begin"));
            cursor.GotoNext(MoveType.After, instr => instr.MatchStloc(out _));

            cursor.Emit(OpCodes.Ldloc_S, curveLocal);
            cursor.Emit(OpCodes.Call, il.Method.DeclaringType.FindMethod("System.Boolean IsVisible(Monocle.SimpleCurve)"));

            // return early if IsVisible returned false
            ILLabel label = cursor.DefineLabel();
            cursor.Emit(OpCodes.Brtrue, label);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(label);
        }
    }
}