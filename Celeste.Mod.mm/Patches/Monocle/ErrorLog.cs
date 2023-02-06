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
using Celeste.Mod.Helpers;

namespace Monocle {
    class patch_ErrorLog {

        public static extern void orig_Write(Exception e);
        public static void Write(Exception e) {
            e.LogDetailed();
            Everest.LogDetours();
            orig_Write(e);
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchErrorLogWrite] // ... except for manually manipulating the method via MonoModRules
        public static extern void Write(string str);

        [MonoModIgnore]
        [PatchErrorLogOpen]
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

    /// <summary>
    /// Find call to Process.Start, and set UseShellExecute flag
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchErrorLogOpen))]
    class PatchErrorLogOpenAttribute : Attribute { }

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

        public static void PatchErrorLogOpen(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);
            cursor.GotoNext(instr => instr.MatchCall("System.Diagnostics.Process", "Start"));
            cursor.Remove();

            /*
            Process proc = new Process() { StartInfo = new ProcessStartInfo(file) { UseShellExecute = true } };
            proc.Start();
            */

            TypeDefinition t_Process = MonoModRule.Modder.FindType("System.Diagnostics.Process").Resolve();
            TypeDefinition t_StartInfo = MonoModRule.Modder.FindType("System.Diagnostics.ProcessStartInfo").Resolve();

            VariableDefinition fileVar = new VariableDefinition(MonoModRule.Modder.FindType("System.String").Resolve());
            context.Body.Variables.Add(fileVar);
            cursor.Emit(OpCodes.Stloc, fileVar);

            // Create new process
            cursor.Emit(OpCodes.Newobj, MonoModRule.Modder.Module.ImportReference(t_Process.FindMethod("System.Void .ctor()")));
            cursor.Emit(OpCodes.Dup);
            cursor.Emit(OpCodes.Dup);

            // Set StartInfo
            cursor.Emit(OpCodes.Ldloc, fileVar);
            cursor.Emit(OpCodes.Newobj, MonoModRule.Modder.Module.ImportReference(t_StartInfo.FindMethod("System.Void .ctor(System.String)")));
            cursor.Emit(OpCodes.Dup);

            cursor.Emit(OpCodes.Ldc_I4_1);
            cursor.Emit(OpCodes.Callvirt, MonoModRule.Modder.Module.ImportReference(t_StartInfo.FindMethod("set_UseShellExecute")));

            cursor.Emit(OpCodes.Callvirt, MonoModRule.Modder.Module.ImportReference(t_Process.FindMethod("set_StartInfo")));

            // Start the process
            cursor.Emit(OpCodes.Callvirt, MonoModRule.Modder.Module.ImportReference(t_Process.FindMethod("System.Boolean Start()")));
            cursor.Emit(OpCodes.Pop);
        }

    }
}
