#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;
using System.Collections;

namespace Celeste {
    class patch_Lookout : Lookout {

        private class patch_Hud : Entity {

            public bool OnlyX;

            [MonoModIgnore]
            [PatchLookoutHudRender]
            public extern override void Render();
        }

        private bool onlyX;

        public patch_Lookout(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [MonoModConstructor]
        [PatchLookoutCtor]
        public extern void ctor(EntityData data, Vector2 offset);

        [MonoModIgnore]
        [PatchLookoutUpdate]
        public extern override void Update();

        [MonoModIgnore]
        [PatchLookoutLookRoutine]
        private extern IEnumerator LookRoutine(Player player);

        // keep for backward compatibility
        public override void SceneEnd(Scene scene) {
            base.SceneEnd(scene);
        }
    }
}

namespace MonoMod {

    /// <summary>
    /// Don't draw vertical arrows if OnlyX is true.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLookoutHudRender))]
    class PatchLookoutHudRenderAttribute : Attribute { }

    /// <summary>
    /// Support `onlyX` attribute.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLookoutCtor))]
    class PatchLookoutCtorAttribute : Attribute { }

    /// <summary>
    /// Don't remove TalkComponent even watchtower collide solid, so that watchtower can be hidden behind Solid.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLookoutUpdate))]
    class PatchLookoutUpdateAttribute : Attribute { }

    /// <summary>
    /// Prevent vertical movement if onlyX is true.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLookoutLookRoutine))]
    class PatchLookoutLookRoutineAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchLookoutHudRender(ILContext context, CustomAttribute attrib) {

            FieldDefinition f_OnlyX = context.Method.DeclaringType.FindField("OnlyX");

            var cursor = new ILCursor(context);

            // Insert:
            //
            // MTexture mTexture = GFX.Gui["towerarrow"]; // local index 6
            //  <-- if (!OnlyX) {
            //     float y = [...];
            //     mTexture.DrawCentered(new Vector2(960f, y), color * up, 1f, MathF.PI / 2f);
            //     float y2 = [...];
            //     mTexture.DrawCentered(new Vector2(960f, y2), color * down, 1f, 3f * MathF.PI / 2f);
            //  <-- }
            // if (!TrackMode && !OnlyY) { ... }

            ILLabel targetLabel = cursor.DefineLabel();

            cursor.GotoNext(MoveType.After, instr => instr.MatchStloc(6)); // mTexture

            cursor.EmitLdarg0(); // this
            cursor.EmitLdfld(f_OnlyX); // this.OnlyX
            cursor.EmitBrtrue(targetLabel); // if (this.OnlyX) goto target;

            cursor.GotoNext(instr => instr.MatchLdfld("Celeste.Lookout/Hud", "TrackMode"));
            cursor.GotoPrev(MoveType.Before, instr => instr.MatchLdarg0());

            cursor.MarkLabel(targetLabel); // target:
        }

        public static void PatchLookoutCtor(ILContext context, CustomAttribute attrib) {

            FieldDefinition f_onlyX = context.Method.DeclaringType.FindField("onlyX");

            TypeDefinition t_EntityData = MonoModRule.Modder.FindType("Celeste.EntityData").Resolve();
            MethodDefinition m_EntityData_Bool = t_EntityData.FindMethod(nameof(EntityData.Bool));

            var cursor = new ILCursor(context);

            // Insert:
            //
            // onlyY = data.Bool("onlyY", false);
            //  <-- onlyX = data.Bool("onlyX", false);

            cursor.GotoNext(MoveType.After, instr => instr.MatchStfld("Celeste.Lookout", "onlyY"));

            cursor.EmitLdarg0(); // this
            cursor.EmitLdarg1(); // this, data
            cursor.EmitLdstr("onlyX"); // this, data, "onlyX"
            cursor.EmitLdcI4(0); // this, data, "onlyX", 0 // same value as false
            cursor.EmitCallvirt(m_EntityData_Bool); // this, data.Bool("onlyX", false)
            cursor.EmitStfld(f_onlyX); // this.onlyX = data.Bool("onlyX", false);
        }

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

        public static void PatchLookoutLookRoutine(MethodDefinition method, CustomAttribute attrib) {

            // we're gonna need to access the real method behind the coroutine
            MethodDefinition moveNext = method.GetEnumeratorMoveNext();

            new ILContext(moveNext).Invoke(context => {

                TypeDefinition declType = method.DeclaringType;

                FieldDefinition f_hud = declType.FindField("hud");
                FieldDefinition f_onlyX = declType.FindField("onlyX");

                TypeDefinition t_Hud = MonoModRule.Modder.FindType("Celeste.Lookout/Hud").Resolve();
                FieldDefinition f_Hud_OnlyX = t_Hud.FindField("OnlyX");

                TypeDefinition t_Vector2 = MonoModRule.Modder.FindType("Microsoft.Xna.Framework.Vector2").Resolve();
                FieldReference f_Vector2_Y = MonoModRule.Modder.Module.ImportReference(t_Vector2.FindField(nameof(Vector2.Y)));

                var cursor = new ILCursor(context);

                // Insert:
                //
                // [IEnumerator code]
                // hud.TrackMode = nodes != null;
                // hud.OnlyY = onlyY;
                //  <-- hud.OnlyX = onlyX;
                //
                // [MoveNext code]
                // Lookout lookout = <>4__this; // local index 1
                // [...]
                // lookout.hud.TrackMode = lookout.nodes != null;
                // lookout.hud.OnlyY = lookout.onlyY;
                //  <-- lookout.hud.OnlyX = lookout.onlyX;

                cursor.GotoNext(MoveType.After, instr => instr.MatchStfld("Celeste.Lookout/Hud", "OnlyY"));

                cursor.EmitLdloc1(); // lookout
                cursor.EmitLdfld(f_hud); // lookout.hud
                cursor.EmitLdloc1(); // lookout.hud, lookout
                cursor.EmitLdfld(f_onlyX); // lookout.hud, lookout.onlyX
                cursor.EmitStfld(f_Hud_OnlyX); // lookout.hud.OnlyX = lookout.onlyX;

                // Insert:
                //
                // [IEnumerator code]
                // Vector2 value = Input.Aim.Value;
                // if (onlyY) value.X = 0f;
                //  <-- if (onlyX) value.Y = 0f;
                //
                // [MoveNext code]
                // Vector2 value = Input.Aim.Value; // local index 6
                // if (lookout.onlyY) value.X = 0f;
                //  <-- if (lookout.onlyX) value.Y = 0f;

                ILLabel targetLabel = null;

                cursor.GotoNext(instr => instr.MatchLdfld("Celeste.Lookout", "onlyY"));
                cursor.GotoNext(instr => instr.MatchBrfalse(out targetLabel));

                cursor.Goto(targetLabel.Target, MoveType.Before);
                cursor.MoveAfterLabels();

                targetLabel = cursor.DefineLabel();

                cursor.EmitLdloc1(); // lookout
                cursor.EmitLdfld(f_onlyX); // lookout.onlyX
                cursor.EmitBrfalse(targetLabel); // if (!lookout.onlyX) goto target;

                cursor.EmitLdloca(6); // ref value
                cursor.EmitLdcR4(0f); // ref value, 0f
                cursor.EmitStfld(f_Vector2_Y); // value.Y = 0f;

                cursor.MarkLabel(targetLabel); // target:
            });
        }

    }
}
