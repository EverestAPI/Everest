using System;
using System.Xml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    class patch_DeathEffect : DeathEffect {

        // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        public patch_DeathEffect(Color color, Vector2 offset)
            : base(color, offset) { }


        public MTexture Texture;

        /// <summary>
        /// a temporary texture used to the next DeathEffect.Draw get call
        /// </summary>
        private static MTexture _texture;

        [MonoModIgnore]
        [PatchDeathEffectUpdate]
        public override extern void Update();

        [MonoModReplace]
        public override void Render() {
            if (Entity != null) {
                _texture = Texture;
                Draw(Entity.Position + Position, Color, Percent);
            }
        }

        [MonoModIgnore]
        [PatchDeathEffectDraw]
        public static new void Draw(Vector2 position, Color color, float ease) { }

        // Because of the auto naming rules of .Hook, we shouldn't name this method same as 'Draw'
        public static void DrawWithTexture(MTexture texture, Vector2 position, Color color, float ease) {
            _texture = texture;
            Draw(position, color, ease);
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch DeathEffect.Update to fix death effects never get removed
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchDeathEffectUpdate))]
    class PatchDeathEffectUpdateAttribute : Attribute { }

    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchDeathEffectDraw))]
    class PatchDeathEffectDrawAttribute : Attribute {
    }

    static partial class MonoModRules {
        public static void PatchDeathEffectUpdate(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(instr => instr.OpCode == OpCodes.Ble_Un_S);
            cursor.Next.OpCode = OpCodes.Blt_Un_S;
        }

        public static void PatchDeathEffectDraw(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_DeathEffect = MonoModRule.Modder.Module.GetType("Celeste.DeathEffect");
            FieldReference m_DeathEffect_texture = t_DeathEffect.FindField("_texture");

            TypeReference t_Color = MonoModRule.Modder.Module.ImportReference(MonoModRule.Modder.FindType("Microsoft.Xna.Framework.Color").Resolve());
            MethodReference m_Color_get_Black = MonoModRule.Modder.Module.ImportReference(t_Color.Resolve().FindProperty("Black").GetMethod);
            VariableDefinition v_outline = new VariableDefinition(t_Color);
            context.Body.Variables.Add(v_outline);

            MethodReference m_Color_get_A = MonoModRule.Modder.Module.ImportReference(t_Color.Resolve().FindProperty("A").GetMethod);
            MethodReference m_Color_op_Multiply = MonoModRule.Modder.Module.ImportReference(t_Color.Resolve().FindMethod("op_Multiply"));
            MethodReference m_Color_op_Inequality = MonoModRule.Modder.Module.ImportReference(t_Color.Resolve().FindMethod("op_Inequality"));

            ILCursor cursor = new ILCursor(context);

            // Color color2 = Color.Black * (color.A / 255f);
            cursor.EmitCall(m_Color_get_Black);
            cursor.EmitLdarga(1);
            cursor.EmitCall(m_Color_get_A);
            cursor.EmitConvR4();
            cursor.EmitLdcR4(255f);
            cursor.EmitDiv();
            cursor.EmitCall(m_Color_op_Multiply);
            cursor.EmitStloc(v_outline);

            // if (color3 != color) { color3 =* (color.A / 255); }
            cursor.GotoNext(MoveType.After, instr => instr.Match(OpCodes.Stloc_0));
            ILLabel IfWhite = cursor.DefineLabel();
            cursor.Emit(OpCodes.Ldloc_0);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.EmitCall(m_Color_op_Inequality);
            cursor.Emit(OpCodes.Brfalse, IfWhite);

            cursor.Emit(OpCodes.Ldloc_0);
            cursor.EmitLdarga(1);
            cursor.EmitCall(m_Color_get_A);
            cursor.EmitConvR4();
            cursor.EmitLdcR4(255f);
            cursor.EmitDiv();
            cursor.EmitCall(m_Color_op_Multiply);
            cursor.Emit(OpCodes.Stloc_0);
            cursor.MarkLabel(IfWhite);

            // GFX.Game["characters/player/hair00"] => (_texture ?? GFX.Game["characters/player/hair00"]);
            ILLabel Ifnull = cursor.DefineLabel();
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdsfld("Celeste.GFX", "Game"));
            cursor.EmitLdsfld(m_DeathEffect_texture);
            cursor.EmitDup();
            cursor.Emit(OpCodes.Brtrue_S, Ifnull);
            cursor.EmitPop();
            cursor.GotoNext(instr => instr.MatchStloc(out int i));
            cursor.MarkLabel(Ifnull);

            // Color.Black => outline;
            for (int i = 4; i > 0; i--) {
                cursor.GotoNext(instr => instr.MatchCall("Microsoft.Xna.Framework.Color", "get_Black"));
                cursor.Remove();
                cursor.EmitLdloc(v_outline);
            }

            // _texture = null;
            cursor.GotoNext(instr => instr.MatchRet());
            cursor.EmitLdnull();
            cursor.EmitStsfld(m_DeathEffect_texture);
        }
    }
}