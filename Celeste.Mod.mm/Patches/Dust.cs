using System;
using Celeste;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.Cil;

namespace Celeste {
    // Dust is static.
    class patch_Dust {
        // Make this signature accessible to older mods.
        public static void Burst(Vector2 position, float direction, int count = 1) {
            Dust.Burst(position, direction, count, null);
        }

        [MonoModIgnore]
        [PatchDustBurst]
        public static extern void Burst(Vector2 position, float direction, int count, ParticleType particleType);

        [MonoModIgnore]
        [PatchDustBurst]
        public static extern void BurstFG(Vector2 position, float direction, int count, float range, ParticleType particleType);

    }
}

namespace MonoMod {

    /// <summary>
    /// Add an early return if Engine.Scene is not a Level, to avoid a crash.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchDustBurst))]
    class PatchDustBurstAttribute : Attribute {}

    static partial class MonoModRules {

        public static void PatchDustBurst(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);

            ILLabel beforeRet = cursor.DefineLabel();
            int loc = -1;

            cursor.GotoNext(MoveType.After,
                            instr => instr.MatchIsinst(out var _),
                            instr => instr.MatchStloc(out loc));
            cursor.EmitLdloc(loc);
            cursor.EmitBrfalse(beforeRet);

            cursor.GotoNext(MoveType.Before,
                            instr => instr.MatchRet());
            cursor.MarkLabel(beforeRet);
        }

    }

}
