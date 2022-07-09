#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using System;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_Lookout : Lookout {

        public patch_Lookout(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchLookoutUpdate]
        public override extern void Update();

        // keep for backward compatibility
        public override void SceneEnd(Scene scene) {
            base.SceneEnd(scene);
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Don't remove TalkComponent even watchtower collide solid, so that watchtower can be hidden behind Solid.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLookoutUpdate))]
    class PatchLookoutUpdateAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchLookoutUpdate(ILContext context, CustomAttribute attrib) {
            FieldReference f_talk = context.Method.DeclaringType.FindField("talk");
            FieldReference f_TalkComponent_UI = context.Method.Module.GetType("Celeste.TalkComponent").FindField("UI");
            FieldReference f_Entity_Visible = context.Method.Module.GetType("Monocle.Entity").FindField("Visible");

            // Remove the following, saving the MethodReference for CollideCheck<Solid>
            // if (this.talk == null || !CollideCheck<Solid>())
            //     return;
            // this.Remove((Component) this.talk);
            // this.talk = (TalkComponent) null;
            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.OpCode == OpCodes.Ldarg_0,
                instr => instr.MatchLdfld("Celeste.Lookout", "talk"));

            MethodReference m_CollideCheck = cursor.Clone().GotoNext(instr => instr.MatchCall("Monocle.Entity", "CollideCheck")).Next.Operand as MethodReference;
            cursor.Next.OpCode = OpCodes.Nop; // This instr may have a break instruction pointing to it
            while (cursor.TryGotoNext(instr => instr.Next != null)) {
                cursor.Remove();
            }

            // Reset to the top and insert
            // if (talk.UI != null) {
            //     talk.UI.Visible = !CollideCheck<Solid>();
            // }
            cursor.Goto(0);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_talk);
            cursor.Emit(OpCodes.Ldfld, f_TalkComponent_UI);
            cursor.Emit(OpCodes.Brfalse_S, cursor.Next);

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_talk);
            cursor.Emit(OpCodes.Ldfld, f_TalkComponent_UI);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Call, m_CollideCheck);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Ceq);
            cursor.Emit(OpCodes.Stfld, f_Entity_Visible);
        }

    }
}
