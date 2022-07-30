using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections;

namespace Celeste {
    public class patch_NPC07X_Granny_Ending : NPC07X_Granny_Ending {
        public patch_NPC07X_Granny_Ending(EntityData data, Vector2 offset, bool ch9EasterEgg = false)
            : base(data, offset, ch9EasterEgg) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchCh9EasterEggText]
        private extern IEnumerator TalkRoutine(Player player);

        private static string _GetDebugModeDialog(string vanillaDialog) {
            if (Dialog.Has("CH10_GRANNY_EASTEREGG")) {
                // dialog key defined by a mod => use this one
                return Dialog.Get("CH10_GRANNY_EASTEREGG");
            }

            // dialog key not defined, like vanilla => use the hardcoded one
            return vanillaDialog;
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Allow a mod to replace the easter egg text by defining it in the language file.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchCh9EasterEggText))]
    class PatchCh9EasterEggText : Attribute { }

    static partial class MonoModRules {
        public static void PatchCh9EasterEggText(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition routine = method.GetEnumeratorMoveNext();

            new ILContext(routine).Invoke(ctx => {
                ILCursor cursor = new ILCursor(ctx);

                if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("{portrait GRANNY right mock} I see you have discovered Debug Mode."))) {
                    return;
                }

                cursor.Emit(OpCodes.Call, method.DeclaringType.FindMethod("System.String _GetDebugModeDialog(System.String)"));
            });
        }
    }
}