using Celeste;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;

namespace Celeste {
    public class patch_ResortMirror : ResortMirror {

        public patch_ResortMirror(EntityData data, Vector2 offset) : base(data, offset) {
            // shut up compiler :)
            // (monomod ignores this anyway)
        }

        // don't do anything except for IL patching ResortMirror.BeforeRender
        // this will prevent a crash if there's an NPC without a sprite set
        [MonoModIgnore]
        [PatchResortMirrorBeforeRender]
        private extern void BeforeRender();
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the <see cref="ResortMirror.BeforeRender"/> method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchResortMirrorBeforeRender))]
    class PatchResortMirrorBeforeRenderAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchResortMirrorBeforeRender(ILContext il, CustomAttribute attr) {
            FieldDefinition f_NPC_Sprite = MonoModRule.Modder.FindType("Celeste.NPC").Resolve().FindField("Sprite");

            ILCursor cursor = new ILCursor(il);
            ILLabel npcIsNull = default;

            if (!cursor.TryGotoNext(MoveType.After,
                static instr => instr.MatchLdloc1(),
                instr => instr.MatchBrfalse(out npcIsNull)))
                throw new Exception("Could not find if (NPC != null) check in ResortMirror.BeforeRender.");

            cursor.EmitLdloc1();
            cursor.EmitLdfld(f_NPC_Sprite);
            cursor.EmitBrfalse(npcIsNull);
        }
    }
}
