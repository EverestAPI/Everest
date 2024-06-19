using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;
using System;

namespace Celeste {
    class patch_Wire : Wire {
        public patch_Wire(EntityData data, Vector2 offset) : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
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
    /// Patches the method to implement culling.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchWireRender))]
    class PatchWireRender : Attribute { }

    static partial class MonoModRules {
        public static void PatchWireRender(ILContext il, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(il);

            // insert culling code after the curve is fully set up.
            cursor.GotoNext(MoveType.After, instr => instr.MatchStfld("Monocle.SimpleCurve", "Control"));

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Call, il.Method.DeclaringType.FindMethod("System.Boolean IsVisible()"));

            // return early if IsVisible returned false
            ILLabel label = cursor.DefineLabel();
            cursor.Emit(OpCodes.Brtrue, label);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(label);
        }
    }
}