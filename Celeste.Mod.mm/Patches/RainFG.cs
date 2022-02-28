using System;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_RainFG : RainFG {

        public new Color? Color;

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchRainFGRender] // ... except for manually manipulating the method via MonoModRules
        public new extern void Render(Scene scene);

        private Color _GetColor(string orig) {
            return Color ?? Calc.HexToColor(orig);
        }

    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the RainFG.Render method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchRainFGRender))]
    class PatchRainFGRenderAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchRainFGRender(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_GetColor = context.Method.DeclaringType.FindMethod("Microsoft.Xna.Framework.Color Celeste.RainFG::_GetColor(System.String)");

            ILCursor cursor = new ILCursor(context);
            // AfterLabel to redirect break instructions
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdstr("161933"));
            // Push `this`.
            cursor.Emit(OpCodes.Ldarg_0);
            // Replace the `Calc.HexToColor` method call after ldstr.
            cursor.Next.Next.Operand = m_GetColor;
        }

    }
}
