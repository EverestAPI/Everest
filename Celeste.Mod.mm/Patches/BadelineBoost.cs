using Microsoft.Xna.Framework;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using System;
using System.Collections;

namespace Celeste {
    class patch_BadelineBoost : BadelineBoost {
        public patch_BadelineBoost(EntityData data, Vector2 offset) : base(data, offset) {
            // no-op, ignored by MonoMod
        }

        [MonoModIgnore]
        [PatchBadelineBoostBoostRoutine]
        private extern IEnumerator BoostRoutine(Player player);
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the BoostRoutine to remove a debug print left by the devs. Fixed in Celeste 1.4.1.0!
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchBadelineBoostBoostRoutine))]
    class PatchBadelineBoostBoostRoutineAttribute : Attribute { }
    static partial class MonoModRules {
        public static void PatchBadelineBoostBoostRoutine(MethodDefinition method, CustomAttribute attrib) {
            // This was removed in 1.4.1.0
            if (CurrentGameVersion >= new Version(1, 4, 1, 0)) return;
            MethodDefinition routine = method.GetEnumeratorMoveNext();
            new ILContext(routine).Invoke(ctx => {
                ILCursor cursor = new ILCursor(ctx);

                // remove Console.WriteLine("TIME: " + sw.ElapsedMilliseconds);
                cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdstr("TIME: "));
                cursor.RemoveRange(7);
            });
        }
    }
}