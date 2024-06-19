using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;

namespace Celeste {
    internal class patch_Solid {
        [MonoModIgnore]
        [PatchSolidAwake]
        public extern void Awake(Scene scene);
    }
}

namespace MonoMod {
    /*
     * Patches the Solid.Awake method to do:
     * if (staticMover.Platform == null && staticMover.IsRiding(this))
     * instead of:
     * if (staticMover.IsRiding(this) && staticMover.Platform == null),
     * 
     * resulting in many less pointless calls to IsRiding.
     */
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchSolidAwake))]
    class PatchSolidAwakeAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchSolidAwake(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_StaticMover = MonoModRule.Modder.FindType("Celeste.StaticMover").Resolve();
            FieldDefinition f_platform = t_StaticMover.FindField("Platform");

            ILCursor cursor = new(context);
            ILLabel label = null;
            // find if (staticMover.IsRiding(this))
            cursor.GotoNext(MoveType.Before,
                instr => instr.MatchLdloc(2),
                instr => instr.MatchLdarg(0),
                instr => instr.MatchCallvirt("Celeste.StaticMover", "IsRiding"),
                instr => instr.MatchBrfalse(out label)
            );

            // insert mover.Platform != null
            cursor.Emit(OpCodes.Ldloc, 2);
            cursor.Emit(OpCodes.Ldfld, f_platform);
            cursor.Emit(OpCodes.Brtrue_S, label);

            // go after the 4 instructions we matched
            cursor.Index += 4;
            // remove the old mover.Platform != null check, as we now perform it earlier
            cursor.RemoveRange(3);
        }
    }
}

