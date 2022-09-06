using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;
using System;

namespace Celeste {
    class patch_BackdropRenderer : BackdropRenderer {
        private bool usingSpritebatch;
        
        public void StartSpritebatchLooping(BlendState blendState) {
            if (!usingSpritebatch) {
                Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, blendState, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, Matrix);
            }
            usingSpritebatch = true;
        }

        [MonoModIgnore]
        [PatchBackdropRendererRender]
        public override extern void Render(Scene scene);
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the method to render parallaxes using a wrapping SamplerState.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchBackdropRendererRender))]
    class PatchBackdropRendererRenderAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchBackdropRendererRender(ILContext context, CustomAttribute attrib) {
            MethodReference m_BackDropRenderer_StartSpritebatchLooping = context.Method.DeclaringType.FindMethod("StartSpritebatchLooping");
            TypeReference t_Parallax = context.Module.GetType("Celeste.Parallax");
            MethodReference m_Parallax_ImprovedRender = t_Parallax.Resolve().FindMethod("ImprovedRender");

            ILCursor cursor = new ILCursor(context);

            /* Change: StartSpritebatch(blendState);
               to: backdrop is Parallax ? StartSpritebatchLooping(blendState) : StartSpritebatch(blendState); */
            cursor.GotoNext(instr => instr.MatchCallvirt("Celeste.BackdropRenderer", "StartSpritebatch"));
            ILLabel beforeStartSpritebatch = cursor.MarkLabel();
            cursor.MoveBeforeLabels();
            cursor.Emit(OpCodes.Ldloc_2); // load backdrop
            cursor.Emit(OpCodes.Isinst, t_Parallax);
            cursor.Emit(OpCodes.Brfalse_S, beforeStartSpritebatch);
            cursor.Emit(OpCodes.Callvirt, m_BackDropRenderer_StartSpritebatchLooping);
            ILLabel nextIf = cursor.DefineLabel();
            cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt("Celeste.BackdropRenderer", "StartSpritebatch"));
            cursor.MarkLabel(nextIf);
            cursor.Index--;
            cursor.Emit(OpCodes.Br, nextIf);

            // call ImprovedRender instead of Render for Parallax
            cursor.GotoNext(instr => instr.MatchLdarg(1), instr => instr.MatchCallvirt("Celeste.Backdrop", "Render"));
            cursor.Emit(OpCodes.Isinst, t_Parallax);
            cursor.Emit(OpCodes.Dup);
            ILLabel parallaxRender = cursor.DefineLabel();
            cursor.Emit(OpCodes.Brtrue_S, parallaxRender);
            cursor.Emit(OpCodes.Pop);
            cursor.Emit(OpCodes.Ldloc_2);
            cursor.Index += 2;
            ILLabel continueLoop = cursor.DefineLabel();
            cursor.Emit(OpCodes.Br, continueLoop);
            cursor.MarkLabel(parallaxRender);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Callvirt, m_Parallax_ImprovedRender);
            cursor.MarkLabel(continueLoop);
        }
    }
}