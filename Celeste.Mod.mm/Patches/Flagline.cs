#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null

using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Mono.Cecil;
using MonoMod.Cil;
using System;
using MonoMod;
using System.Linq;
using MonoMod.Utils;
using Monocle;
using Celeste.Mod.Helpers;

namespace Celeste {
    class patch_Flagline : Flagline {
        public patch_Flagline(Vector2 to, Color lineColor, Color pinColor, Color[] colors, int minFlagHeight, int maxFlagHeight, int minFlagLength, int maxFlagLength, int minSpace, int maxSpace) 
            : base(to, lineColor, pinColor, colors, minFlagHeight, maxFlagHeight, minFlagLength, maxFlagLength, minSpace, maxSpace) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchFlaglineDraw]
        public override extern void Render();

        private bool IsVisible(SimpleCurve curve) {
            Cloth[] clothes = this.clothes;
            float maxHeight = 0f;
            for (int i = 0; i < clothes.Length; i++) {
                Cloth cloth = clothes[i];
                float h = cloth.Height + (cloth.Length * ClothDroopAmount * 1.4f);

                if (h > maxHeight) {
                    maxHeight = h;
                }
            }

            return CullHelper.IsCurveVisible(curve, maxHeight + 8f);
        }

        [MonoModIgnore]
        private Cloth[] clothes;

        [MonoModIgnore]
        private struct Cloth {
            public int Color;

            public int Height;

            public int Length;

            public int Step;
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the method to implement culling.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchFlaglineDraw))]
    class PatchFlaglineDraw : Attribute { }

    static partial class MonoModRules {
        public static void PatchFlaglineDraw(ILContext il, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(il);

            VariableDefinition curveLocal = cursor.Body.Variables.First(v => v.VariableType.Name.Contains("SimpleCurve"));

            cursor.GotoNext(MoveType.After, instr => instr.MatchCall("Monocle.SimpleCurve", ".ctor"));

            cursor.Emit(OpCodes.Ldarg_0);
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