using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;
using System;

namespace Celeste {
    class patch_Wire : Wire {
        private bool fixWindBehavior;

        public patch_Wire(EntityData data, Vector2 offset) : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModConstructor]
        [MonoModReplace]
        public void ctor(EntityData data, Vector2 offset) {
            ctor(data.Position + offset, data.Nodes[0] + offset, data.Bool("above"), data.Bool("fixWindBehavior"));
        }

        [MonoModConstructor]
        [MonoModIgnore]
        public extern void ctor(Vector2 from, Vector2 to, bool above);

        [MonoModConstructor]
        public void ctor(Vector2 from, Vector2 to, bool above, bool fixWindBehavior) {
            ctor(from, to, above);
            this.fixWindBehavior = fixWindBehavior;
        }

        [MonoModIgnore]
        [PatchWireRender]
        public extern override void Render();

        private bool IsVisible() {
            return CullHelper.IsCurveVisible(Curve, 2f);
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the method to implement culling and reduce the wind render offset on wires.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchWireRender))]
    class PatchWireRender : Attribute { }

    static partial class MonoModRules {
        public static void PatchWireRender(ILContext il, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(il);

            // from empirical testing this seemed about the right amount to lower the wind amplitude by
            const float windAmplitudeReduction = 100f;

            // lower the VisualWind amplitude
            cursor.GotoNext(MoveType.After, static instr => instr.MatchCallvirt("Celeste.Level", "get_VisualWind"));
            ILLabel noDiv = cursor.DefineLabel();
            cursor.EmitLdarg0();
            cursor.EmitLdfld(il.Method.DeclaringType.FindField("fixWindBehavior"));
            cursor.EmitBrfalse(noDiv);
            cursor.EmitLdcR4(windAmplitudeReduction);
            cursor.EmitDiv();
            cursor.MarkLabel(noDiv);

            // insert culling code after the curve is fully set up.
            cursor.GotoNext(MoveType.After, static instr => instr.MatchStfld("Monocle.SimpleCurve", "Control"));

            cursor.EmitLdarg0();
            cursor.EmitCall(il.Method.DeclaringType.FindMethod("System.Boolean IsVisible()"));

            // return early if IsVisible returned false
            ILLabel label = cursor.DefineLabel();
            cursor.EmitBrtrue(label);
            cursor.EmitRet();
            cursor.MarkLabel(label);
        }
    }
}
