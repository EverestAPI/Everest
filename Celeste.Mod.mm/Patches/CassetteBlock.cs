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
    class patch_CassetteBlock : CassetteBlock {

        public patch_CassetteBlock(EntityData data, Vector2 offset, EntityID id)
            : base(data, offset, id) {
        }

        // 1.3.0.0 gets rid of the 2-arg ctor.
        // We're adding a new ctor, thus can't call the constructor without a small workaround.
        [MonoModLinkTo("Celeste.CassetteBlock", "System.Void .ctor(Celeste.EntityData,Microsoft.Xna.Framework.Vector2,Celeste.EntityID)")]
        [MonoModForceCall]
        [MonoModRemove]
        public extern void ctor(EntityData data, Vector2 offset, EntityID id);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset) {
            ctor(data, offset, new EntityID(data.Level.Name, data.ID));
        }

        [MonoModIgnore]
        [PatchCassetteBlockAwake]
        public override extern void Awake(Scene scene);
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches <see cref="Celeste.CassetteBlock.Awake(Monocle.Scene)" /> to fix issue #334.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchCassetteBlockAwake))]
    class PatchCassetteBlockAwakeAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchCassetteBlockAwake(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);

            FieldReference f_Entity_Collidable = MonoModRule.Modder.Module.GetType("Monocle.Entity").FindField("Collidable");
            MethodReference m_Platform_DisableStaticMovers = MonoModRule.Modder.Module.GetType("Celeste.Platform").FindMethod("System.Void DisableStaticMovers()");

            cursor.GotoNext(MoveType.AfterLabel,
                instr => instr.MatchLdarg(0),
                instr => instr.MatchCallOrCallvirt("Celeste.CassetteBlock", "System.Void UpdateVisualState()"));

            Instruction target = cursor.Next;

            // add if (!Collidable) { DisableStaticMovers(); } before UpdateVisualState()
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_Entity_Collidable);
            cursor.Emit(OpCodes.Brtrue, target);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Call, m_Platform_DisableStaticMovers);
        }

    }
}
