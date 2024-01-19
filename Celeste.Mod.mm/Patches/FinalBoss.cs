#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
﻿#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using System;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_FinalBoss : FinalBoss {

        private bool canChangeMusic;

        private int nodeIndex;

        private Vector2[] nodes;

        public patch_FinalBoss(EntityData e, Vector2 offset)
            : base(e, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(EntityData data, Vector2 offset);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset) {
            orig_ctor(data, offset);

            canChangeMusic = data.Bool("canChangeMusic", true);
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchBadelineBossOnPlayer] // ... except for manually manipulating the method via MonoModRules
        public new extern void OnPlayer(Player player);

        private extern void orig_PushPlayer(Player player);

        private void PushPlayer(Player player) {
            // ensure we are not hitting the last node
            if (nodeIndex >= nodes.Length) {
                Logger.Warn("FinalBoss",
                    "FinalBoss entity was hit on its last node, please add an additional node outside of the current room to ensure the player never hits it.");
                patch_LevelEnter.ErrorMessage = Dialog.Get("postcard_bosslastnodehit");
            } else {
                orig_PushPlayer(player);
            }
        }

        public bool CanChangeMusic(bool value) {
            Level level = Scene as Level;
            if (level.Session.Area.GetLevelSet() == "Celeste")
                return value;

            return canChangeMusic;
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the Badeline boss OnPlayer method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchBadelineBossOnPlayer))]
    class PatchBadelineBossOnPlayerAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchBadelineBossOnPlayer(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_CanChangeMusic = context.Method.DeclaringType.FindMethod("System.Boolean Celeste.FinalBoss::CanChangeMusic(System.Boolean)");

            ILCursor cursor = new ILCursor(context);

            cursor.GotoNext(MoveType.After, instr => instr.MatchLdfld("Celeste.AreaKey", "Mode"));
            // Insert `== 0`
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Ceq);
            // Replace brtrue with brfalse
            cursor.Next.OpCode = OpCodes.Brfalse_S;

            // Process.
            cursor.Emit(OpCodes.Call, m_CanChangeMusic);

            // Go back to the start of this "line" and add `this` to be used by CanChangeMusic()
            cursor.GotoPrev(instr => instr.OpCode == OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_0);
        }

    }
}
