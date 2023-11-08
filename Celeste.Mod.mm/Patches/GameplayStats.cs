using Microsoft.Xna.Framework;
using Mono.Cecil;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Linq;
using Mono.Cecil.Cil;
using MonoMod;

namespace Celeste {
    public class patch_GameplayStats : GameplayStats {
        [MonoModIgnore]
        [WrapBerryTracker]
        public extern new void Render();

        private static float lastLineOffset;
        private static int remainingLineCount;

        private static void getInitialPosition(ref Vector2 orig) {
            remainingLineCount = 0;

            if (orig.X >= 32f) {
                return;
            }

            while (orig.X < 32f) {
                orig.X += (1920f - 32f) / 2f;
                orig.Y -= 48f;

                remainingLineCount++;
            }

            lastLineOffset = orig.X;
            orig.X = 32f;
        }

        private static void wrapPosition(ref Vector2 orig) {
            if (orig.X > 1920f - 32f) {
                orig.X = 32f;
                orig.Y += 48f;

                remainingLineCount--;
                if (remainingLineCount < 1) {
                    orig.X = lastLineOffset;
                }
            }
        }
    }
}


namespace MonoMod {
    [MonoModCustomMethodAttribute(nameof(MonoModRules.WrapBerryTracker))]
    class WrapBerryTrackerAttribute : Attribute { }

    static partial class MonoModRules {
        public static void WrapBerryTracker(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);

            int vectorIndex = context.Body.Variables.Where(var => var.VariableType.FullName == "Microsoft.Xna.Framework.Vector2").First().Index;
            MethodDefinition m_getInitialPosition = context.Method.DeclaringType.FindMethod("getInitialPosition");
            MethodDefinition m_wrapPosition = context.Method.DeclaringType.FindMethod("wrapPosition");

            cursor.GotoNext(MoveType.After, instr => instr.OpCode == OpCodes.Call && (instr.Operand as MethodReference).FullName == "System.Void Microsoft.Xna.Framework.Vector2::.ctor(System.Single,System.Single)");
            cursor.Emit(OpCodes.Ldloca, vectorIndex);
            cursor.Emit(OpCodes.Call, m_getInitialPosition);

            for (int i = 0; i < 2; i++) {
                cursor.GotoNext(MoveType.After, instr => instr.MatchStindR4());
                cursor.Emit(OpCodes.Ldloca, vectorIndex);
                cursor.Emit(OpCodes.Call, m_wrapPosition);
            }
        }
    }
}