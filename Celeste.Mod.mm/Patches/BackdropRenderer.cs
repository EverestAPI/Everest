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
    /// Patches the method to begin a wrapping spritebatch for parallaxes.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchBackdropRendererRender))]
    class PatchBackdropRendererRenderAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchBackdropRendererRender(ILContext context, CustomAttribute attrib) {
            MethodReference m_BackDropRenderer_StartSpritebatchLooping = context.Method.DeclaringType.FindMethod("StartSpritebatchLooping");
            TypeReference t_Parallax = context.Module.GetType("Celeste.Parallax");
            
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
        }
    }
}