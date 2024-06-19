using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using System;

namespace Celeste {
    class patch_NPC06_Badeline_Crying : NPC06_Badeline_Crying {
        public patch_NPC06_Badeline_Crying(EntityData data, Vector2 offset) : base(data, offset) {
            // no-op, ignored by MonoMod
        }

        [MonoModConstructor]
        [MonoModIgnore]
        [PatchNPC06BadelineCryingCtor]
        public extern void ctor(EntityData data, Vector2 offset);
    }
}

namespace MonoMod {

    /// <summary>
    /// Patch the constructor to use event:/none instead of other nonexistent event.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchNPC06BadelineCryingCtor))]
    class PatchNPC06BadelineCryingCtorAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchNPC06BadelineCryingCtor(ILContext il, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(il);

            // use conventional fallback event instead of nonexistent event event:/char/badeline/boss_idle_ground
            cursor.GotoNext(instr => instr.MatchLdstr("event:/char/badeline/boss_idle_ground"));
            cursor.Remove();
            cursor.Emit(OpCodes.Ldstr, "event:/none");
        }
    }
}