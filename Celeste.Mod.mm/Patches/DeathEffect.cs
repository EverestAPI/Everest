using System;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;

namespace Celeste {
    class patch_DeathEffect : DeathEffect {

        public patch_DeathEffect(Color color, Vector2 offset)
            : base(color, offset) { }

        [MonoModIgnore]
        [PatchDeathEffectUpdate]
        public override extern void Update();

        [MonoModReplace]
        public override void Render() {
            if (Entity != null)
                Draw(Entity.Position + Position, Color, Percent);
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch DeathEffect.Update to fix death effects never get removed
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchDeathEffectUpdate))]
    class PatchDeathEffectUpdateAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchDeathEffectUpdate(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(instr => instr.OpCode == OpCodes.Ble_Un_S);
            cursor.Next.OpCode = OpCodes.Blt_Un_S;
        }

    }
}
