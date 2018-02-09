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

        static TypeDefinition Celeste;

        static TypeDefinition FileProxy;
        static IDictionary<string, MethodDefinition> FileProxyCache = new FastDictionary<string, MethodDefinition>();

        static MonoModRules() {
            Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "0");

            if (Celeste == null)
                Celeste = MMILRT.Modder.FindType("Celeste.Celeste")?.Resolve();
            if (Celeste == null)
                return;

            // Get version - used to set any MonoMod flags.

            string versionString = null;
            int[] versionInts = null;
            // Find Celeste .ctor (luckily only has one)
            MethodDefinition c_Celeste = Celeste.FindMethod(".ctor", true);
            if (c_Celeste != null && c_Celeste.HasBody) {
                Mono.Collections.Generic.Collection<Instruction> instrs = c_Celeste.Body.Instructions;
                for (int instri = 0; instri < instrs.Count; instri++) {
                    Instruction instr = instrs[instri];
                    MethodReference c_Version = instr.Operand as MethodReference;
                    if (instr.OpCode != OpCodes.Newobj || c_Version?.DeclaringType?.FullName != "System.Version")
                        continue;

                    // We're constructing a System.Version - check if all parameters are of type int.
                    bool c_Version_intsOnly = true;
                    foreach (ParameterReference param in c_Version.Parameters)
                        if (param.ParameterType.MetadataType != MetadataType.Int32) {
                            c_Version_intsOnly = false;
                            break;
                        }

                    if (c_Version_intsOnly) {
                        // Assume that ldc.i4* instructions are right before the newobj.
                        versionInts = new int[c_Version.Parameters.Count];
                        for (int i = -versionInts.Length; i < 0; i++)
                            versionInts[i + versionInts.Length] = instrs[i + instri].GetInt();
                    }

                    if (c_Version.Parameters.Count == 1 && c_Version.Parameters[0].ParameterType.MetadataType == MetadataType.String) {
                        // Assume that a ldstr is right before the newobj.
                        versionString = instrs[instri - 1].Operand as string;
                    }

                    // Don't check any other instructions.
                    break;
                }
            }

            // Construct the version from our gathered data.
            Version version = new Version();
            if (versionString != null) {
                version = new Version(versionString);
            } if (versionInts == null || versionInts.Length == 0) {
                // ???
            } else if (versionInts.Length == 2) {
                version = new Version(versionInts[0], versionInts[1]);
            } else if (versionInts.Length == 3) {
                version = new Version(versionInts[0], versionInts[1], versionInts[2]);
            } else if (versionInts.Length == 4) {
                version = new Version(versionInts[0], versionInts[1], versionInts[2], versionInts[3]);
            }

            // Set any flags based on the version.
            if (version < new Version(1, 1, 9, 2)) {
                MMIL.Flag.Set("LacksIntroSkip", true);
                MMIL.Flag.Set("HasIntroSkip", false);

            } else {
                // Current version.
                MMIL.Flag.Set("LacksIntroSkip", false);
                MMIL.Flag.Set("HasIntroSkip", true);
            }

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
                    FileProxyCache[calling.GetFindableID(withType: false)] = replacement = FileProxy.FindMethod(calling.GetFindableID(withType: false));
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

                if (pop && instr.OpCode == OpCodes.Throw) {
                    instr.OpCode = OpCodes.Pop;
                    pop = false;
                }
            }

        }

    }
}
