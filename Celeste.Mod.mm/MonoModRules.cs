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
    /// Proxy any System.IO.File.* calls inside the method via Celeste.Mod.Helpers.FileProxy.*
    /// </summary>
    [MonoModCustomMethodAttribute("ProxyFileCalls")]
    class ProxyFileCallsAttribute : Attribute { }

    /// <summary>
    /// Check for ldstr "Corrupted Level Data" and pop the throw after that.
    /// Also manually execute ProxyFileCalls rule.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchMapDataLoader")]
    class PatchMapDataLoaderAttribute : Attribute { }

    /// <summary>
    /// Patch the Godzilla-sized level loading method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchLevelLoader")]
    class PatchLevelLoaderAttribute : Attribute { }

    /// <summary>
    /// Patch the Godzilla-sized level rendering method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchLevelRender")]
    class PatchLevelRenderAttribute : Attribute { }

    /// <summary>
    /// Patch the Godzilla-sized level loading thread method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchLevelLoaderThread")]
    class PatchLevelLoaderThreadAttribute : Attribute { }

    /// <summary>
    /// Find ldfld Engine::Version + ToString. Pop ToString result, call Everest::get_VersionCelesteString
    /// </summary>
    [MonoModCustomMethodAttribute("PatchErrorLogWrite")]
    class PatchErrorLogWriteAttribute : Attribute { }

    /// <summary>
    /// Slap a ldfld completeMeta right before newobj AreaComplete
    /// </summary>
    [MonoModCustomMethodAttribute("RegisterLevelExitRoutine")]
    class PatchLevelExitRoutineAttribute : Attribute { }

    /// <summary>
    /// Slap a MapMetaCompleteScreen param at the end of the constructor and ldarg it right before newobj CompleteRenderer
    /// </summary>
    [MonoModCustomMethodAttribute("RegisterAreaCompleteCtor")]
    class PatchAreaCompleteCtorAttribute : Attribute { }

    class MonoModRules {

        static TypeDefinition Celeste;

        static TypeDefinition Everest;
        static MethodDefinition m_Everest_get_VersionCelesteString;

        static TypeDefinition Level;

        static TypeDefinition FileProxy;
        static IDictionary<string, MethodDefinition> FileProxyCache = new FastDictionary<string, MethodDefinition>();

        static List<MethodDefinition> LevelExitRoutines = new List<MethodDefinition>();
        static List<MethodDefinition> AreaCompleteCtors = new List<MethodDefinition>();

        static MonoModRules() {
            Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "0");

            MonoModRule.Modder.PostProcessors += PostProcessor;

            if (Celeste == null)
                Celeste = MonoModRule.Modder.FindType("Celeste.Celeste")?.Resolve();
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
                MonoModRule.Flag.Set("LacksIntroSkip", true);
                MonoModRule.Flag.Set("HasIntroSkip", false);

            } else {
                // Current version.
                MonoModRule.Flag.Set("LacksIntroSkip", false);
                MonoModRule.Flag.Set("HasIntroSkip", true);
            }

        }

        public static void ProxyFileCalls(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            if (FileProxy == null)
                FileProxy = MonoModRule.Modder.FindType("Celeste.Mod.Helpers.FileProxy")?.Resolve();
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

        public static void PatchMapDataLoader(MethodDefinition method, CustomAttribute attrib) {
            // Our actual target method is the orig_ method.
            method = method.DeclaringType.FindMethod(method.GetFindableID(name: method.GetOriginalName()));

            if (!method.HasBody)
                return;

            ProxyFileCalls(method, attrib);

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

        public static void PatchLevelLoader(MethodDefinition method, CustomAttribute attrib) {
            // Our actual target method is the orig_ method.
            method = method.DeclaringType.FindMethod(method.GetFindableID(name: method.GetOriginalName()));

            if (!method.HasBody)
                return;

            MethodDefinition m_LoadCustomEntity = method.DeclaringType.FindMethod("System.Boolean LoadCustomEntity(Celeste.EntityData,Celeste.Level)");
            if (m_LoadCustomEntity == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                /* We expect something similar enough to the following:
                ldwhatever the entityData into stack
                ldfld     string Celeste.EntityData::Name // We're here
				stloc*
				ldloc*
				call      uint32 '<PrivateImplementationDetails>'::ComputeStringHash(string)

                Note that MonoMod requires the full type names (System.UInt32 instead of uint32) and skips escaping 's
                */

                if (instri > 0 &&
                    instri < instrs.Count - 4 &&
                    instr.OpCode == OpCodes.Ldfld && (instr.Operand as FieldReference)?.FullName == "System.String Celeste.EntityData::Name" &&
                    instrs[instri + 1].OpCode.Name.ToLowerInvariant().StartsWith("stloc") &&
                    instrs[instri + 2].OpCode.Name.ToLowerInvariant().StartsWith("ldloc") &&
                    instrs[instri + 3].OpCode == OpCodes.Call && (instrs[instri + 3].Operand as MethodReference)?.GetFindableID() == "System.UInt32 <PrivateImplementationDetails>::ComputeStringHash(System.String)"
                ) {
                    // Insert a call to our own entity handler here.
                    // If it returns true, replace the name with ""

                    // Avoid loading entityData again.
                    // Instead, duplicate already loaded existing value.
                    instrs.Insert(instri, il.Create(OpCodes.Dup));
                    instri++;
                    // Load "this" onto stack - we're too lazy to shift this to the beginning of the stack.
                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;

                    // Call our static custom entity handler.
                    instrs.Insert(instri, il.Create(OpCodes.Call, m_LoadCustomEntity));
                    instri++;

                    // If we returned false, branch to ldfld. We still have the entity name on stack.
                    // This basically translates to if (result) { pop; ldstr ""; }; ldfld ...
                    instrs.Insert(instri, il.Create(OpCodes.Brfalse_S, instrs[instri]));
                    instri++;
                    // Otherwise, pop the entityData, load "" and jump to stloc to skip any original entity handler.
                    instrs.Insert(instri, il.Create(OpCodes.Pop));
                    instri++;
                    instrs.Insert(instri, il.Create(OpCodes.Ldstr, ""));
                    instri++;
                    instrs.Insert(instri, il.Create(OpCodes.Br_S, instrs[instri + 1]));
                    instri++;
                }

            }

        }

        public static void PatchLevelRender(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            FieldDefinition f_SubHudRenderer = method.DeclaringType.FindField("SubHudRenderer");
            if (f_SubHudRenderer == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                /* We expect something similar enough to the following:
                brfalse.s	338 (0441) ldarg.0 
                ldarg.0
                ldfld	class Celeste.HudRenderer Celeste.Level::HudRenderer // We're here
                ldarg.0
                callvirt	instance void Monocle.Renderer::Render(class Monocle.Scene)

                Note that MonoMod requires the full type names (System.UInt32 instead of uint32) and skips escaping 's
                */

                if (instri > 1 &&
                    instri < instrs.Count - 2 &&
                    instr.OpCode == OpCodes.Ldfld && (instr.Operand as FieldReference)?.FullName == "Celeste.HudRenderer Celeste.Level::HudRenderer" &&
                    instrs[instri + 1].OpCode == OpCodes.Ldarg_0 &&
                    instrs[instri + 2].OpCode == OpCodes.Callvirt && (instrs[instri + 2].Operand as MethodReference)?.GetFindableID() == "System.Void Monocle.Renderer::Render(Monocle.Scene)"
                ) {
                    // Load this, SubHudRenderer, this and call it right before the branch.

                    MethodReference m_Renderer_Render = instrs[instri + 2].Operand as MethodReference;

                    instri -= 2;

                    // Let's go back before ldarg.0, System.Boolean Monocle.Scene::Paused
                    int pausedOffs = 0;
                    while (
                        instri > 0 &&
                        !(instrs[instri].OpCode == OpCodes.Ldfld && (instrs[instri].Operand as FieldReference)?.FullName == "System.Boolean Monocle.Scene::Paused")
                    ) {
                        instri--;
                        pausedOffs++;
                    }

                    // We use the existing ldarg.0
                    instrs.Insert(instri, il.Create(OpCodes.Ldfld, f_SubHudRenderer));
                    instri++;

                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;

                    instrs.Insert(instri, il.Create(OpCodes.Callvirt, m_Renderer_Render));
                    instri++;

                    // Add back the ldarg.0 which we consumed.
                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;

                    instri += pausedOffs;
                    instri += 2;
                }

            }

        }

        public static void PatchLevelLoaderThread(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            if (Level == null)
                Level = MonoModRule.Modder.FindType("Celeste.Level")?.Resolve();
            if (Level == null)
                return;

            FieldDefinition f_SubHudRenderer = Level.FindField("SubHudRenderer");
            if (f_SubHudRenderer == null)
                return;

            MethodDefinition ctor_SubHudRenderer = f_SubHudRenderer.FieldType.Resolve()?.FindMethod("System.Void .ctor()");
            if (ctor_SubHudRenderer == null)
                return;

            VariableDefinition loc_SubHudRenderer_0 = new VariableDefinition(f_SubHudRenderer.FieldType);
            method.Body.Variables.Add(loc_SubHudRenderer_0);

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                /* We expect something similar enough to the following:
        	    ldarg.0
                callvirt	instance class Celeste.Level Celeste.LevelLoader::get_Level()
                ldarg.0
                callvirt	instance class Celeste.Level Celeste.LevelLoader::get_Level()
                newobj	instance void Celeste.HudRenderer::.ctor() // We're here
                dup
                stloc.s	V_9 (9)
                stfld	class Celeste.HudRenderer Celeste.Level::HudRenderer
                ldloc.s	V_9 (9)
                callvirt	instance void Monocle.Scene::Add(class Monocle.Renderer)

                Note that MonoMod requires the full type names (System.UInt32 instead of uint32) and skips escaping 's
                */

                if (instri > 3 &&
                    instri < instrs.Count - 6 &&
                    instr.OpCode == OpCodes.Newobj && (instr.Operand as MethodReference)?.GetFindableID() == "System.Void Celeste.HudRenderer::.ctor()" &&
                    instrs[instri + 1].OpCode == OpCodes.Dup &&
                    instrs[instri + 2].OpCode.Name.ToLowerInvariant().StartsWith("stloc") &&
                    instrs[instri + 3].OpCode == OpCodes.Stfld && (instrs[instri + 3].Operand as FieldReference)?.FullName == "Celeste.HudRenderer Celeste.Level::HudRenderer" &&
                    instrs[instri + 4].OpCode.Name.ToLowerInvariant().StartsWith("ldloc") &&
                    instrs[instri + 5].OpCode == OpCodes.Callvirt && (instrs[instri + 5].Operand as MethodReference)?.GetFindableID() == "System.Void Monocle.Scene::Add(Monocle.Renderer)"
                ) {
                    // Insert our own SubHudRenderer here.

                    // Avoid calling get_Level again.
                    // Instead, duplicate already loaded existing value.
                    instrs.Insert(instri, il.Create(OpCodes.Dup)); // Used to Add()
                    instri++;
                    instrs.Insert(instri, il.Create(OpCodes.Dup)); // Used to stfld
                    instri++;

                    // Load the new renderer onto the stack.
                    instrs.Insert(instri, il.Create(OpCodes.Newobj, ctor_SubHudRenderer));
                    instri++;

                    // Store the renderer in a local so we can first stfld, then Add() it.
                    instrs.Insert(instri, il.Create(OpCodes.Dup));
                    instri++;
                    instrs.Insert(instri, il.Create(OpCodes.Stloc, loc_SubHudRenderer_0));
                    instri++;

                    // Store the renderer in the level.
                    instrs.Insert(instri, il.Create(OpCodes.Stfld, f_SubHudRenderer));
                    instri++;

                    // Add the renderer to the scene.
                    instrs.Insert(instri, il.Create(OpCodes.Ldloc, loc_SubHudRenderer_0));
                    instri++;
                    // Offset taken from above.
                    instrs.Insert(instri, il.Create(OpCodes.Callvirt, instrs[instri + 5].Operand as MethodReference));
                    instri++;

                    // The rest should work as-is.
                }

            }

        }

        public static void PatchErrorLogWrite(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            if (Everest == null)
                Everest = MonoModRule.Modder.FindType("Celeste.Mod.Everest")?.Resolve();
            if (Everest == null)
                return;

            if (m_Everest_get_VersionCelesteString == null)
                m_Everest_get_VersionCelesteString = Everest.FindMethod("System.String get_VersionCelesteString()");
            if (m_Everest_get_VersionCelesteString == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                /* We expect something similar enough to the following:
                ldfld     class [mscorlib] System.Version Monocle.Engine::Version // We're here
                callvirt instance string[mscorlib] System.Object::ToString()

                Note that MonoMod requires the full type names (System.String instead of string)
                */

                if (instri > 0 &&
                    instri < instrs.Count - 3 &&
                    instr.OpCode == OpCodes.Ldfld && (instr.Operand as FieldReference)?.FullName == "System.Version Monocle.Engine::Version" &&
                    instrs[instri + 1].OpCode == OpCodes.Callvirt && (instrs[instri + 1].Operand as MethodReference)?.GetFindableID() == "System.String System.Object::ToString()"
                ) {
                    // Skip the ldfld Version and ToString instructions.
                    instri += 2;

                    // Pop and replace with our own string.
                    instrs.Insert(instri, il.Create(OpCodes.Pop));
                    instri++;
                    instrs.Insert(instri, il.Create(OpCodes.Call, m_Everest_get_VersionCelesteString));
                    instri++;
                }

            }

        }

        public static void RegisterLevelExitRoutine(MethodDefinition method, CustomAttribute attrib) {
            // Register it. Don't patch it directly as we require an explicit patching order.
            LevelExitRoutines.Add(method);
        }

        public static void PatchLevelExitRoutine(MethodDefinition method) {
            FieldDefinition f_completeMeta = method.DeclaringType.FindField("completeMeta");
            FieldDefinition f_this = null;

            // The level exit routine is stored in a compiler-generated method.
            foreach (TypeDefinition nest in method.DeclaringType.NestedTypes) {
                if (!nest.Name.StartsWith("<" + method.Name + ">d__"))
                    continue;
                method = nest.FindMethod("System.Boolean MoveNext()") ?? method;
                f_this = method.DeclaringType.FindField("<>4__this");
                break;
            }

            if (!method.HasBody)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];
                MethodReference calling = instr.Operand as MethodReference;
                string callingID = calling?.GetFindableID();

                // The original AreaComplete .ctor has been modified to contain an extra parameter.
                // For safety, check against both signatures.
                if (instr.OpCode != OpCodes.Newobj || (
                    callingID != "System.Void Celeste.AreaComplete::.ctor(Celeste.Session,System.Xml.XmlElement,Monocle.Atlas,Celeste.HiresSnow)" &&
                    callingID != "System.Void Celeste.AreaComplete::.ctor(Celeste.Session,System.Xml.XmlElement,Monocle.Atlas,Celeste.HiresSnow,Celeste.Mod.Meta.MapMetaCompleteScreen)"
                ))
                    continue;

                // For safety, replace the .ctor call if the new .ctor exists already.
                instr.Operand = calling.DeclaringType.Resolve().FindMethod("System.Void Celeste.AreaComplete::.ctor(Celeste.Session,System.Xml.XmlElement,Monocle.Atlas,Celeste.HiresSnow,Celeste.Mod.Meta.MapMetaCompleteScreen)") ?? instr.Operand;

                instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                instri++;

                if (f_this != null) {
                    instrs.Insert(instri, il.Create(OpCodes.Ldfld, f_this));
                    instri++;
                }

                instrs.Insert(instri, il.Create(OpCodes.Ldfld, f_completeMeta));
                instri++;

            }

        }

        public static void RegisterAreaCompleteCtor(MethodDefinition method, CustomAttribute attrib) {
            // Register it. Don't patch it directly as we require an explicit patching order.
            AreaCompleteCtors.Add(method);
        }

        public static void PatchAreaCompleteCtor(MethodDefinition method) {
            if (!method.HasBody)
                return;

            ParameterDefinition paramMeta = new ParameterDefinition("meta", ParameterAttributes.None, MonoModRule.Modder.FindType("Celeste.Mod.Meta.MapMetaCompleteScreen"));
            method.Parameters.Add(paramMeta);

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];
                MethodReference calling = instr.Operand as MethodReference;
                string callingID = calling?.GetFindableID();

                // The matching CompleteRenderer .ctor has been added manually, thus manually relink to it.
                if (instr.OpCode != OpCodes.Newobj || (
                    callingID != "System.Void Celeste.CompleteRenderer::.ctor(System.Xml.XmlElement,Monocle.Atlas,System.Single,System.Action)"
                ))
                    continue;

                instr.Operand = calling.DeclaringType.Resolve().FindMethod("System.Void Celeste.CompleteRenderer::.ctor(System.Xml.XmlElement,Monocle.Atlas,System.Single,System.Action,Celeste.Mod.Meta.MapMetaCompleteScreen)");

                instrs.Insert(instri, il.Create(OpCodes.Ldarg, paramMeta));
                instri++;
            }

        }

        public static void PostProcessor(MonoModder modder) {
            // Patch previously registered AreaCompleteCtors and LevelExitRoutines _in that order._
            foreach (MethodDefinition method in AreaCompleteCtors)
                PatchAreaCompleteCtor(method);
            foreach (MethodDefinition method in LevelExitRoutines)
                PatchLevelExitRoutine(method);

        }

    }
}
