#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    class patch_CrushBlock : CrushBlock {

        public patch_CrushBlock(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchCrushBlockAttackSequence]
        private extern IEnumerator AttackSequence();

    }
}

namespace MonoMod {
    /// <summary>
    /// Patches CrushBlock.AttackSequence to fix chillout crush blocks traversing too short gaps crashing the game.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchCrushBlockAttackSequence))]
    class PatchCrushBlockAttackSequenceAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchCrushBlockAttackSequence(MethodDefinition method, CustomAttribute attrib) {
            method = MonoModRule.Modder.Module.GetType("Celeste.CrushBlock/<>c__DisplayClass41_0").FindMethod("<AttackSequence>b__1");
            new ILContext(method).Invoke(il => {
                ILCursor cursor = new ILCursor(il);

                // this.currentMoveLoopSfx.Stop(true);
                // TO
                // this.currentMoveLoopSfx?.Stop(true);
                cursor.GotoNext(MoveType.After, instr => instr.MatchLdfld("Celeste.CrushBlock", "currentMoveLoopSfx"));
                Instruction instrPop = cursor.Clone().GotoNext(instr => instr.MatchPop()).Next;
                cursor.Emit(OpCodes.Dup);
                cursor.Emit(OpCodes.Brfalse_S, instrPop);
            });
        }

    }
}
