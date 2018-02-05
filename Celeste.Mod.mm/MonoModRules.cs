using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Helpers;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoMod {
    /// <summary>
    /// Proxy any System.IO.File.* calls inside the method via Celeste.Mod.FileProxy.*
    /// </summary>
    [MonoModCustomMethodAttribute("ProxyFileCalls")]
    class ProxyFileCallsAttribute : Attribute { }
    /// <summary>
    /// Check for ldstr "Corrupted Level Data" and pop the throw after that.
    /// </summary>
    [MonoModCustomMethodAttribute("PopCorruptedLevelData")]
    class PopCorruptedLevelDataAttribute : Attribute { }
    class MonoModRules {

        static TypeDefinition FileProxy;
        static IDictionary<string, MethodDefinition> FileProxyCache = new FastDictionary<string, MethodDefinition>();

        static MonoModRules() {
            Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "0");
        }

        public static void ProxyFileCalls(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            if (FileProxy == null)
                FileProxy = MMILRT.Modder.FindType("Celeste.Mod.FileProxy")?.Resolve();
            if (FileProxy == null)
                return;

            foreach (Instruction instr in method.Body.Instructions) {
                // System.IO.File.* calls are always static calls.
                if (instr.OpCode != OpCodes.Call)
                    continue;

                // We only want to replace System.IO.File.* calls.
                MethodReference calling = instr.Operand as MethodReference;
                if (calling?.DeclaringType?.FullName != "System.IO.File")
                    continue;

                MethodDefinition replacement;
                if (!FileProxyCache.TryGetValue(calling.Name, out replacement))
                    FileProxyCache[calling.Name] = replacement = FileProxy.FindMethod(calling.Name);
                if (replacement == null)
                    continue; // We haven't got any replacement.

                // Replace the called method with our replacement.
                instr.Operand = replacement;
            }

        }

        public static void PopCorruptedLevelData(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            bool pop = false;
            foreach (Instruction instr in method.Body.Instructions) {
                if (instr.OpCode == OpCodes.Ldstr && (instr.Operand as string) == "Corrupted Level Data")
                    pop = true;

                if (pop && instr.OpCode == OpCodes.Throw)
                    instr.OpCode = OpCodes.Pop;
            }

        }

    }
}
