using System;
using System.Collections;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_MiniTextbox : MiniTextbox {

        public patch_MiniTextbox(string dialogId)
            : base(dialogId) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchMiniTextboxRoutine]
        private extern IEnumerator Routine();

    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the method to fix mini textbox not closing when it's expanding and another textbox is triggered.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchMiniTextboxRoutine))]
    class PatchMiniTextboxRoutine : Attribute { }

    static partial class MonoModRules {

        public static void PatchMiniTextboxRoutine(MethodDefinition method, CustomAttribute attrib) {
            FieldDefinition f_MiniTextbox_closing = method.DeclaringType.FindField("closing");

            // The routine is stored in a compiler-generated method.
            method = method.GetEnumeratorMoveNext();

            new ILContext(method).Invoke(il => {
                ILCursor cursor = new ILCursor(il);

                /*
                    Change:

                        while ((this.ease += Engine.DeltaTime * 4f) < 1f)) {
                            continueLoopTarget:
                            yield return null;
                        }
                        this.ease = 1f;

                    to:

                        while ((this.ease += Engine.DeltaTime * 4f) < 1f)) {
                            continueLoopTarget:
                            if (this.closing) {
                                yield break;
                            }
                            yieldReturnNullTarget:
                            yield return null;
                        }
                        this.ease = 1f;
                */
                ILLabel continueLoopTarget = cursor.DefineLabel();
                cursor.GotoNext(MoveType.After,
                    instr => instr.MatchLdloc(6),
                    instr => instr.MatchLdcR4(1f),
                    instr => instr.MatchBlt(out continueLoopTarget));

                cursor.Goto(continueLoopTarget.Target, MoveType.AfterLabel);

                ILLabel yieldReturnNullTarget = cursor.DefineLabel();
                cursor.Emit(OpCodes.Ldloc_1);
                cursor.Emit(OpCodes.Ldfld, f_MiniTextbox_closing);
                cursor.Emit(OpCodes.Brfalse, yieldReturnNullTarget);
                cursor.Emit(OpCodes.Ldc_I4_0);
                cursor.Emit(OpCodes.Ret);
                cursor.MarkLabel(yieldReturnNullTarget);
            });
        }

    }
}
