#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using Celeste;
using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    class patch_Glitch {

        [MonoModIgnore] 
        [PatchGlitchPhotosensitivity] // Don't change anything, but manipulate it using an IL patch (fun!)
        public extern static void Apply(VirtualRenderTarget source, float timer, float seed, float amplitude);        
    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the Glitch effect to render based on Everest's advanced photosensitivity settings.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchGlitchPhotosensitivity))]
    class PatchGlitchPhotosensitivityAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchGlitchPhotosensitivity(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_CoreModule = MonoModRule.Modder.Module.GetType("Celeste.Mod.Core.CoreModule");
            TypeDefinition t_CoreModuleSettings = MonoModRule.Modder.Module.GetType("Celeste.Mod.Core.CoreModuleSettings");
            MethodDefinition m_CoreModule_get_Settings = t_CoreModule.FindMethod("get_Settings");
            MethodDefinition m_get_AllowGlitch = t_CoreModuleSettings.FindMethod("get_AllowGlitch");

            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(MoveType.Before, instr => instr.MatchLdsfld("Celeste.Settings", "Instance"));
            cursor.RemoveRange(2);
            cursor.EmitCall(m_CoreModule_get_Settings);
            cursor.EmitCallvirt(m_get_AllowGlitch);
            cursor.Next.OpCode = OpCodes.Brfalse;
        }
    }
}
