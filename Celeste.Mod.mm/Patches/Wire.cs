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

        // from empirical testing 100f seemed about the right amount to lower the wind amplitude by
        private float ReducedVisualWind =>
            Scene is Level level ? level.Wind.X / 100f + level.WindSine : 0f;

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

            TypeDefinition t_Wire = il.Method.DeclaringType;

            // use ReducedVisualWind if necessary
            cursor.GotoNext(MoveType.Before,
                static instr => instr.MatchLdloc(0),
                static instr => instr.MatchCallvirt("Celeste.Level", "get_VisualWind"));
            ILLabel needsFix = cursor.DefineLabel();
            ILLabel join = cursor.DefineLabel();

            // fixWindBehavior ?
            cursor.EmitLdarg0();
            cursor.EmitLdfld(t_Wire.FindField("fixWindBehavior"));
            cursor.EmitBrtrue(needsFix);

            // : level.VisualWind
            cursor.Index += 2;
            cursor.EmitBr(join);

            // ReducedVisualWind :
            cursor.MarkLabel(needsFix);
            cursor.EmitLdarg0();
            cursor.EmitCallvirt(t_Wire.FindProperty("ReducedVisualWind").GetMethod);
            cursor.MarkLabel(join);

            // insert culling code after the curve is fully set up.
            cursor.GotoNext(MoveType.After, static instr => instr.MatchStfld("Monocle.SimpleCurve", "Control"));

            cursor.EmitLdarg0();
            cursor.EmitCall(t_Wire.FindMethod("System.Boolean IsVisible()"));

            // return early if IsVisible returned false
            ILLabel label = cursor.DefineLabel();
            cursor.EmitBrtrue(label);
            cursor.EmitRet();
            cursor.MarkLabel(label);
        }
    }
}
