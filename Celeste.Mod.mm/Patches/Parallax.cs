using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;
using System;

namespace Celeste {
    class patch_Parallax : Parallax {
        
        public patch_Parallax(MTexture texture) : base(texture) {
            // no-op, ignored by MonoMod
        }

        private void DrawParallax(Vector2 position, Color color, SpriteEffects flip) {
            Rectangle rect = new Rectangle(0, 0, LoopX ? Celeste.GameWidth : Texture.Width, LoopY ? Celeste.GameHeight : Texture.Height);
            ((patch_MTexture) Texture).DrawWithWrappingSupport(position, Vector2.Zero, color, 1f, 0f, flip, rect);
        }

        [MonoModIgnore]
        [PatchParallaxRender]
        public override extern void Render(Scene scene);
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the method to replace looped Draw calls with single call.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchParallaxRender))]
    class PatchParallaxRenderAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchParallaxRender(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_Parallax_DrawParallax = context.Method.DeclaringType.FindMethod("DrawParallax");

            ILCursor cursor = new ILCursor(context);

            cursor.GotoNext(MoveType.After, instr => instr.MatchLdfld("Celeste.Backdrop", "FlipY"),
                                            instr => instr.MatchBrfalse(out _),
                                            instr => instr.MatchLdcI4(2),
                                            instr => instr.MatchStloc(4),
                                            instr => instr.MatchLdloc(1),
                                            instr => instr.MatchLdfld("Microsoft.Xna.Framework.Vector2", "X"));
            cursor.Index -= 2;
            cursor.MoveAfterLabels();
            cursor.RemoveRange(cursor.Instrs.Count - cursor.Index - 1); // delete rest of method except ret instruction
            cursor.MoveAfterLabels();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc_1); // position
            cursor.Emit(OpCodes.Ldloc_3); // color
            cursor.Emit(OpCodes.Ldloc_S, (byte) 4); // flip
            cursor.Emit(OpCodes.Callvirt, m_Parallax_DrawParallax);
        }
    }
}