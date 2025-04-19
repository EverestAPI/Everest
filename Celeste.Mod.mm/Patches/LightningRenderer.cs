#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using System.Collections;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    class patch_LightningRenderer : LightningRenderer {

        private class patch_Bolt {

            [MonoModIgnore]
            [PatchLightningRendererBoltRun]
            private extern IEnumerator Run();

            [MonoModIgnore]
            [PatchLightningRendererBoltRender]
            public extern void Render();
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches LightningRenderer.Bolt.Run to respect advanced photosensitivity settings.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLightningRendererBoltRun))]
    class PatchLightningRendererBoltRunAttribute : Attribute { }

    /// <summary>
    /// Patches LightningRenderer.Bolt.Render to respect advanced photosensitivity settings.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchLightningRendererBoltRender))]
    class PatchLightningRendererBoltRenderAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchLightningRendererBoltRun(MethodDefinition method, CustomAttribute attrib) {
            // The method to patch
            MethodDefinition runRoutine = method.GetEnumeratorMoveNext();

            // All of the core module stuff (fun!)
            TypeDefinition t_CoreModule = MonoModRule.Modder.Module.GetType("Celeste.Mod.Core.CoreModule");
            TypeDefinition t_CoreModuleSettings = MonoModRule.Modder.Module.GetType("Celeste.Mod.Core.CoreModuleSettings");
            MethodDefinition m_CoreModule_get_Settings = t_CoreModule.FindMethod("get_Settings");
            MethodDefinition m_get_AllowLightning = t_CoreModuleSettings.FindMethod("get_AllowLightning");;
            
            // Code stolen from CrushBlock and Glitch and then very slightly modified
            new ILContext(runRoutine).Invoke(il => {
                ILCursor cursor = new ILCursor(il);

                cursor.GotoNext(MoveType.Before, instr => instr.MatchLdsfld("Celeste.Settings", "Instance"));
                
                // Put the settings module on the stack
                cursor.Next.OpCode = OpCodes.Call;
                cursor.Next.Operand = m_CoreModule_get_Settings;

                // Get the AllowLightning value 
                cursor.Index++;
                cursor.Next.OpCode = OpCodes.Callvirt;
                cursor.Next.Operand = m_get_AllowLightning;

                // Break if false, instead of if true
                cursor.Index++;
                cursor.Next.OpCode = OpCodes.Brfalse;
            });
        }

        public static void PatchLightningRendererBoltRender(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_CoreModule = MonoModRule.Modder.Module.GetType("Celeste.Mod.Core.CoreModule");
            TypeDefinition t_CoreModuleSettings = MonoModRule.Modder.Module.GetType("Celeste.Mod.Core.CoreModuleSettings");

            // Get the getters manually because properties scare me
            MethodDefinition m_CoreModule_get_Settings = t_CoreModule.FindMethod("get_Settings");
            MethodDefinition m_get_AllowLightning = t_CoreModuleSettings.FindMethod("get_AllowLightning");

            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(MoveType.Before, instr => instr.MatchLdsfld("Celeste.Settings", "Instance"));

            // Put the settings module on the stack
            cursor.Next.OpCode = OpCodes.Call;
            cursor.Next.Operand = m_CoreModule_get_Settings;

            // Get the AllowLightning value 
            cursor.Index++;
            cursor.Next.OpCode = OpCodes.Call;
            cursor.Next.Operand = m_get_AllowLightning;

            // Break if false, instead of if true
            cursor.Index++;
            cursor.Next.OpCode = OpCodes.Brfalse;
        }
    }
}
