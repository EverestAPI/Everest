using Celeste;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using System;
using System.Collections;

namespace Celeste {
    public class patch_CS10_FinalLaunch : CS10_FinalLaunch {
        public patch_CS10_FinalLaunch(Player player, BadelineBoost boost, bool sayDialog = true)
            : base(player, boost, sayDialog) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchFinalLaunchCutscene]
        private extern IEnumerator Cutscene();
    }
}

namespace MonoMod {
    /// <summary>
    /// A patch for the Chapter 9 cutscene, which adds a null check around the <see cref="BlackholeBG" />
    /// for use in modded maps which don't have a black hole styleground.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchFinalLaunchCutscene))]
    class PatchFinalLaunchCutsceneAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchFinalLaunchCutscene(MethodDefinition method, CustomAttribute attrib) {
            method = method.GetEnumeratorMoveNext();

            new ILContext(method).Invoke(static (il) => {
                ILCursor cursor = new(il);

                ILLabel ifNull = cursor.DefineLabel();
                ILLabel done = cursor.DefineLabel();

                cursor.GotoNext(MoveType.After,
                    static instr => instr.MatchCallvirt("Celeste.BackdropRenderer", "Get"));

                // if (blackholeBg is not null)
                // {
                cursor.EmitDup();
                cursor.EmitBrfalse(ifNull);

                //   ...
                cursor.GotoNext(MoveType.After,
                    static instr => instr.MatchLdflda("Celeste.BlackholeBG", "OffsetOffset"),
                    static instr => instr.MatchLdcR4(-50),
                    static instr => instr.MatchStfld("Microsoft.Xna.Framework.Vector2", "Y"));

                // }
                cursor.EmitBr(done);
                cursor.MarkLabel(ifNull);

                // if the ifNull branch is taken,
                // there's still an instance of BlackholeBG on the stack
                cursor.EmitPop();
                cursor.MarkLabel(done);
            });
        }
    }
}
