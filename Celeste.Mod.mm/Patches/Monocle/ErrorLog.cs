#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Core;
using MonoMod;
using MonoMod.Utils;
using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.InlineRT;

namespace Monocle {
    class patch_ErrorLog {

        public static extern void orig_Write(Exception e);
        public static void Write(Exception e) {
            Logger.LogDetailed(e);
            Everest.LogDetours();
            orig_Write(e);
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchErrorLogWrite] // ... except for manually manipulating the method via MonoModRules
        public static extern void Write(string str);

        public static extern void orig_Open();
        public static void Open() {
            if (Environment.GetEnvironmentVariable("EVEREST_NO_ERRORLOG_ON_CRASH") != "1" && CoreModule.Settings.OpenErrorLogOnCrash)
                orig_Open();
        }

    }
}

namespace MonoMod {
    /// <summary>
    /// Find ldfld Engine::Version + ToString. Pop ToString result, call Everest::get_VersionCelesteString
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchErrorLogWrite))]
    class PatchErrorLogWriteAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchErrorLogWrite(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_Everest = MonoModRule.Modder.FindType("Celeste.Mod.Everest").Resolve();
            MethodDefinition m_Everest_get_VersionCelesteString = t_Everest.FindMethod("System.String get_VersionCelesteString()");

            /* We expect something similar enough to the following:
            call     class Monocle.Engine Monocle.Engine::get_Instance() // We're here
            ldfld    class [mscorlib] System.Version Monocle.Engine::Version 
            callvirt instance string[mscorlib] System.Object::ToString() 

            Note that MonoMod requires the full type names (System.String instead of string)
            */

            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(instr => instr.MatchCall("Monocle.Engine", "get_Instance"),
                instr => instr.MatchLdfld("Monocle.Engine", "Version"),
                instr => instr.MatchCallvirt("System.Object", "ToString"));

            // Remove all that and replace with our own string.
            cursor.RemoveRange(3);
            cursor.Emit(OpCodes.Call, m_Everest_get_VersionCelesteString);
        }

    }
}
