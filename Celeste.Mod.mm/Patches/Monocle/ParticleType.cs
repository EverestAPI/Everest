using MonoMod;
using System;
using System.Collections.Generic;
using Mono.Cecil;
using MonoMod.Cil;

namespace Monocle {
    class patch_ParticleType : ParticleType
    {
        [Obsolete("Unused. ParticleType instances are not added automatically.")]
        private static List<ParticleType> AllTypes;

        [MonoModIgnore]
        [MonoModConstructor]
        [PatchParticleTypeCtor]
        public extern void ctor();

        [MonoModIgnore]
        [MonoModConstructor]
        [PatchParticleTypeCtor]
        public extern void ctor(ParticleType copyFrom);
    }
}

namespace MonoMod {
    /// <summary>
    ///   Patches the <see cref="Monocle.ParticleType"/> constructor to prevent it from adding all instances to an unused static list, which would keep them all alive forever.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchParticleTypeCtor))]
    class PatchParticleTypeCtorAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchParticleTypeCtor(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new(context);

            // remove the `AllTypes.Add(this);` call
            cursor.GotoNext(MoveType.Before, instr => instr.MatchLdsfld("Monocle.ParticleType", "AllTypes"));
            cursor.RemoveRange(3);
        }
    }
}
