using System;
using Microsoft.Xna.Framework;
using MonoMod;
using System.Collections;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    class patch_MoveBlock : MoveBlock {

        public patch_MoveBlock(Vector2 position, int width, int height, MoveBlock.Directions direction, bool canSteer, bool fast)
            : base(position, width, height, direction, canSteer, fast) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchMoveBlockController]
        private extern IEnumerator Controller();
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches MoveBlock.Controller to disable static movers before resetting their position when breaking.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchMoveBlockController))]
    class PatchMoveBlockControllerAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchMoveBlockController(MethodDefinition method, CustomAttribute attrib) {
            MethodReference m_Platform_MoveStaticMovers = MonoModRule.Modder.Module.GetType("Celeste.Platform").FindMethod("System.Void MoveStaticMovers(Microsoft.Xna.Framework.Vector2)");

            // The routine is stored in a compiler-generated method.
            method = method.GetEnumeratorMoveNext();

            new ILContext(method).Invoke(il => {
                ILCursor cursor = new ILCursor(il);

                // From:
                //     this.MoveStaticMovers(this.startPosition - this.Position);
                //     this.DisableStaticMovers();
                // To:
                //     Vector2 amount = this.startPosition - this.Position;
                //     this.DisableStaticMovers();
                //     this.MoveStaticMovers(amount);
                cursor.GotoNext(MoveType.Before, instr => instr.MatchCallvirt("Celeste.Platform", "MoveStaticMovers"));
                cursor.Remove();
                cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt("Celeste.Platform", "DisableStaticMovers"));

                // The argument order happens to let us emit the two function calls adjacent to each other
                cursor.Emit(OpCodes.Callvirt, m_Platform_MoveStaticMovers);
            });

        }

    }
}
