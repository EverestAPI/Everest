#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    class patch_GoldenBlock : GoldenBlock {

#pragma warning disable CS0649 // field is never assigned and will always be null: it is initialized in vanilla code
        private float renderLerp;
#pragma warning restore CS0649

        public patch_GoldenBlock(EntityData data, Vector2 offset) : base(data, offset) {
            // no-op.
        }

        public extern void orig_Update();
        public override void Update() {
            orig_Update();
            if (renderLerp == 0)
                EnableStaticMovers();
            else
                DisableStaticMovers();
        }

        [MonoModIgnore] // we don't want to change anything in the method...
        [PatchGoldenBlockStaticMovers] // ... except manipulating it manually with MonoModRules
        public extern override void Awake(Scene scene);
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches GoldenBlocks to disable static movers if the block is disabled.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchGoldenBlockStaticMovers))]
    class PatchGoldenBlockStaticMoversAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchGoldenBlockStaticMovers(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_Platform = MonoModRule.Modder.Module.GetType("Celeste.Platform");
            MethodDefinition m_Platform_DisableStaticMovers = t_Platform.FindMethod("System.Void DisableStaticMovers()");
            MethodDefinition m_Platform_DestroyStaticMovers = t_Platform.FindMethod("System.Void DestroyStaticMovers()");

            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(MoveType.After, instr => instr.MatchCall("Celeste.Solid", "Awake"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Call, m_Platform_DisableStaticMovers);

            cursor.GotoNext(instr => instr.MatchCall("Monocle.Entity", "RemoveSelf"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Call, m_Platform_DestroyStaticMovers);
        }

    }
}
