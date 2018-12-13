using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
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
    /// Check for ldstr "Unhandled SDL2 platform!" and pop the throw after that.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchUnhandledSDL2Platform")]
    class PatchUnhandledSDL2PlatformAttribute : Attribute { }

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
    /// Patch the Godzilla-sized level updating method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchLevelUpdate")]
    class PatchLevelUpdateAttribute : Attribute { }

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
    /// Patch the heart gem collection routine instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchHeartGemCollectRoutine")]
    class PatchHeartGemCollectRoutineAttribute : Attribute { }

    /// <summary>
    /// Patch the Badeline chase routine instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchBadelineChaseRoutine")]
    class PatchBadelineChaseRoutineAttribute : Attribute { }

    /// <summary>
    /// Patch the Cloud.Added method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchCloudAdded")]
    class PatchCloudAddedAttribute : Attribute { }

    /// <summary>
    /// Patch the Dialog.LoadLanguage method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchLoadLanguage")]
    class PatchLoadLanguageAttribute : Attribute { }

    /// <summary>
    /// Patch the Dialog.InitLanguages method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchInitLanguages")]
    class PatchInitLanguagesAttribute : Attribute { }

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

    static class MonoModRules {

        static bool IsCeleste;

        static bool FMODStub = false;

        static TypeDefinition Celeste;

        static TypeDefinition Everest;
        static MethodDefinition m_Everest_get_VersionCelesteString;

        static TypeDefinition Level;

        static TypeDefinition FileProxy;
        static IDictionary<string, MethodDefinition> FileProxyCache = new Dictionary<string, MethodDefinition>();

        static List<MethodDefinition> LevelExitRoutines = new List<MethodDefinition>();
        static List<MethodDefinition> AreaCompleteCtors = new List<MethodDefinition>();

        static MonoModRules() {
            // Note: It may actually be too late to set this to false.
            MonoModRule.Modder.MissingDependencyThrow = false;

            FMODStub = Environment.GetEnvironmentVariable("EVEREST_FMOD_STUB") == "1";
            MonoModRule.Flag.Set("FMODStub", FMODStub);

            bool isFNA = false;
            foreach (AssemblyNameReference name in MonoModRule.Modder.Module.AssemblyReferences)
                if (isFNA = name.Name.Contains("FNA"))
                    break;
            MonoModRule.Flag.Set("FNA", isFNA);

            if (Celeste == null)
                Celeste = MonoModRule.Modder.FindType("Celeste.Celeste")?.Resolve();
            if (Celeste == null)
                return;
            IsCeleste = Celeste.Scope == MonoModRule.Modder.Module;

            if (IsCeleste) {
                MonoModRule.Modder.PostProcessors += PostProcessor;
            }

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

            // Set up any flags.

            if (version < new Version(1, 1, 9, 2)) {
                MonoModRule.Flag.Set("Lacks:IntroSkip", true);
                MonoModRule.Flag.Set("Has:IntroSkip", false);
            } else {
                MonoModRule.Flag.Set("Lacks:IntroSkip", false);
                MonoModRule.Flag.Set("Has:IntroSkip", true);
            }

            MonoModRule.Flag.Set("Fill:SpeedrunType", MonoModRule.Modder.FindType("Celeste.SpeedrunType")?.SafeResolve() == null);

            TypeDefinition settings = MonoModRule.Modder.FindType("Celeste.Settings").Resolve();
            MonoModRule.Flag.Set("Fill:LaunchInDebugMode", settings.FindField("LaunchInDebugMode")?.SafeResolve() == null);
            MonoModRule.Flag.Set("Fill:LaunchWithFMODLiveUpdate", settings.FindField("LaunchWithFMODLiveUpdate")?.SafeResolve() == null);

            TypeDefinition userio = MonoModRule.Modder.FindType("Celeste.UserIO").Resolve();
            MethodDefinition userio_load = userio.FindMethod("Load");
            MonoModRule.Flag.Set("V1:UserIOLoad", userio_load.Parameters.Count == 1);
            MonoModRule.Flag.Set("V2:UserIOLoad", userio_load.Parameters.Count == 2);
            MethodDefinition userio_saveroutine = userio.FindMethod("SaveRoutine");
            MonoModRule.Flag.Set("V1:UserIOSave", userio_saveroutine == null);
            MonoModRule.Flag.Set("V2:UserIOSave", userio_saveroutine != null);

            TypeDefinition playerhair = MonoModRule.Modder.FindType("Celeste.PlayerHair").Resolve();
            FieldDefinition playerhair_sprite = userio.FindField("Sprite");
            MonoModRule.Flag.Set("V1:PlayerHairSprite", playerhair_sprite == null);
            MonoModRule.Flag.Set("V2:PlayerHairSprite", playerhair_sprite != null);

            TypeDefinition cassetteblock = MonoModRule.Modder.FindType("Celeste.CassetteBlock").Resolve();
            MethodDefinition cassetteblock_ctor = cassetteblock.FindMethod(".ctor");
            MonoModRule.Flag.Set("V1:CassetteBlockCtor", cassetteblock_ctor.Parameters.Count == 4);
            MonoModRule.Flag.Set("V2:CassetteBlockCtor", cassetteblock_ctor.Parameters.Count == 5);
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

        public static void PatchUnhandledSDL2Platform(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            bool pop = false;
            foreach (Instruction instr in method.Body.Instructions) {
                if (instr.OpCode == OpCodes.Ldstr && (instr.Operand as string) == "Unhandled SDL2 platform!")
                    pop = true;

                if (pop && instr.OpCode == OpCodes.Throw) {
                    instr.OpCode = OpCodes.Pop;
                    pop = false;
                }
            }

        }

        public static void PatchMapDataLoader(MethodDefinition method, CustomAttribute attrib) {
            // Our actual target method is the orig_ method.
            method = method.DeclaringType.FindMethod(method.GetFindableID(name: method.GetOriginalName()));

            if (!method.HasBody)
                return;

            ProxyFileCalls(method, attrib);

            MethodDefinition m_Process = method.DeclaringType.FindMethod("Celeste.BinaryPacker/Element _Process(Celeste.BinaryPacker/Element,Celeste.MapData)");
            if (m_Process == null)
                return;

            MethodDefinition m_GrowAndGet = method.DeclaringType.FindMethod("Celeste.EntityData _GrowAndGet(Celeste.EntityData[,]&,System.Int32,System.Int32)");
            if (m_GrowAndGet == null)
                return;

            bool pop = false;
            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Ldstr && (instr.Operand as string) == "Corrupted Level Data")
                    pop = true;

                if (pop && instr.OpCode == OpCodes.Throw) {
                    instr.OpCode = OpCodes.Pop;
                    pop = false;
                }

                if (instr.OpCode == OpCodes.Call && (instr.Operand as MethodReference)?.GetFindableID() == "Celeste.BinaryPacker/Element Celeste.BinaryPacker::FromBinary(System.String)") {
                    instri++;

                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;
                    instrs.Insert(instri, il.Create(OpCodes.Call, m_Process));
                    instri++;
                }

                if (instri > 2 &&
                    instrs[instri - 3].OpCode == OpCodes.Ldfld && (instrs[instri - 3].Operand as FieldReference)?.FullName == "Celeste.EntityData[,] Celeste.ModeProperties::StrawberriesByCheckpoint" &&
                    instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference)?.GetFindableID() == "Celeste.EntityData Celeste.EntityData[,]::Get(System.Int32,System.Int32)"
                ) {
                    instrs[instri - 3].OpCode = OpCodes.Ldflda;
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_GrowAndGet;
                    instri++;
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

        public static void PatchLevelUpdate(MethodDefinition method, CustomAttribute attrib) {
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
                call class Monocle.MInput/KeyboardData Monocle.MInput::get_Keyboard()
                ldc.i4.s 9
                callvirt instance bool Monocle.MInput/KeyboardData::Pressed(valuetype [FNA]Microsoft.Xna.Framework.Input.Keys) // We're here

                Note that MonoMod requires the full type names (System.UInt32 instead of uint32) and skips escaping 's
                */

                if (instri > 1 &&
                    instri < instrs.Count - 2 &&
                    instrs[instri - 2].OpCode == OpCodes.Call && (instrs[instri - 2].Operand as MethodReference)?.GetFindableID() == "Monocle.MInput/KeyboardData Monocle.MInput::get_Keyboard()" &&
                    instrs[instri - 1].GetIntOrNull() == 9 &&
                    instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference)?.GetFindableID() == "System.Boolean Monocle.MInput/KeyboardData::Pressed(Microsoft.Xna.Framework.Input.Keys)"
                ) {
                    // Replace the offending instructions with a ldc.i4.0
                    instri -= 2;

                    instrs.RemoveAt(instri);
                    instrs.RemoveAt(instri);
                    instrs.RemoveAt(instri);
                    instrs.Insert(instri, il.Create(OpCodes.Ldc_I4_0));
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
                brfalse.s    338 (0441) ldarg.0 
                ldarg.0
                ldfld    class Celeste.HudRenderer Celeste.Level::HudRenderer // We're here
                ldarg.0
                callvirt    instance void Monocle.Renderer::Render(class Monocle.Scene)

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
                callvirt    instance class Celeste.Level Celeste.LevelLoader::get_Level()
                ldarg.0
                callvirt    instance class Celeste.Level Celeste.LevelLoader::get_Level()
                newobj    instance void Celeste.HudRenderer::.ctor() // We're here
                dup
                stloc.s    V_9 (9)
                stfld    class Celeste.HudRenderer Celeste.Level::HudRenderer
                ldloc.s    V_9 (9)
                callvirt    instance void Monocle.Scene::Add(class Monocle.Renderer)

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

        public static void PatchHeartGemCollectRoutine(MethodDefinition method, CustomAttribute attrib) {
            FieldDefinition f_this = null;
            FieldDefinition f_completeArea = null;

            MethodDefinition m_IsCompleteArea = method.DeclaringType.FindMethod("System.Boolean IsCompleteArea(System.Boolean)");
            if (m_IsCompleteArea == null)
                return;

            // The gem collection routine is stored in a compiler-generated method.
            foreach (TypeDefinition nest in method.DeclaringType.NestedTypes) {
                if (!nest.Name.StartsWith("<" + method.Name + ">d__"))
                    continue;
                method = nest.FindMethod("System.Boolean MoveNext()") ?? method;
                f_this = method.DeclaringType.FindField("<>4__this");
                f_completeArea = method.DeclaringType.Fields.FirstOrDefault(f => f.Name.StartsWith("<completeArea>5__"));
                break;
            }

            if (!method.HasBody || f_completeArea == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                // Pre-process the bool on stack before
                // stfld    bool Celeste.HeartGem/'<CollectRoutine>d__29'::'<completeArea>5__4'
                // No need to check for the full name when the field name itself is compiler-generated.
                if (instr.OpCode == OpCodes.Stfld && (instr.Operand as FieldReference)?.Name == f_completeArea.Name
                ) {
                    // After stfld, grab the result, process it, store.
                    instri++;
                    // Push this on stack and duplicate, keeping a copy for stfld later.
                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;
                    instrs.Insert(instri, il.Create(OpCodes.Dup));
                    instri++;
                    // Grab this from this.
                    instrs.Insert(instri, il.Create(OpCodes.Ldfld, f_this));
                    instri++;
                    // Push completeArea on stack.
                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;
                    instrs.Insert(instri, il.Create(OpCodes.Ldfld, f_completeArea));
                    instri++;
                    // Process.
                    instrs.Insert(instri, il.Create(OpCodes.Call, m_IsCompleteArea));
                    instri++;
                    // Store.
                    instrs.Insert(instri, il.Create(OpCodes.Stfld, f_completeArea));
                    instri++;
                }

            }

        }

        public static void PatchBadelineChaseRoutine(MethodDefinition method, CustomAttribute attrib) {
            FieldDefinition f_this = null;

            MethodDefinition m_IsChaseEnd = method.DeclaringType.FindMethod("System.Boolean _IsChaseEnd(System.Boolean,Celeste.BadelineOldsite)");
            if (m_IsChaseEnd == null)
                return;

            // The routine is stored in a compiler-generated method.
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


                /* We expect something similar enough to the following:
                ldfld    string Celeste.Session::Level
                ldstr    "2"
                call    bool [mscorlib]System.String::op_Equality(string, string) // We're here

                Note that MonoMod requires the full type names (System.String instead of string)
                */
                // No need to check for the full name when the field name itself is compiler-generated.
                if (instri > 3 &&
                    instrs[instri - 2].OpCode == OpCodes.Ldfld && (instrs[instri - 2].Operand as FieldReference)?.FullName == "System.String Celeste.Session::Level" &&
                    instrs[instri - 1].OpCode == OpCodes.Ldstr && (instrs[instri - 1].Operand as string) == "2" &&
                    instr.OpCode == OpCodes.Call && (instr.Operand as MethodReference)?.GetFindableID() == "System.Boolean System.String::op_Equality(System.String,System.String)"
                ) {
                    // After ==, process the result.
                    instri++;
                    // Push this and grab this from this.
                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;
                    instrs.Insert(instri, il.Create(OpCodes.Ldfld, f_this));
                    instri++;
                    // Process.
                    instrs.Insert(instri, il.Create(OpCodes.Call, m_IsChaseEnd));
                    instri++;
                }

            }

        }

        public static void PatchCloudAdded(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            MethodDefinition m_IsSmall = method.DeclaringType.FindMethod("System.Boolean _IsSmall(System.Boolean,Celeste.Cloud)");
            if (m_IsSmall == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                /* We expect something similar enough to the following:
                ldfld    Celeste.AreaMode Celeste.AreaKey::Mode
                ldc.i4.0
                cgt.un    // We're here

                Note that MonoMod requires the full type names (System.String instead of string)
                */
                // No need to check for the full name when the field name itself is compiler-generated.
                if (instri > 3 &&
                    instrs[instri - 2].OpCode == OpCodes.Ldfld && (instrs[instri - 2].Operand as FieldReference)?.FullName == "Celeste.AreaMode Celeste.AreaKey::Mode" &&
                    instrs[instri - 1].OpCode == OpCodes.Ldc_I4_0 &&
                    instr.OpCode == OpCodes.Cgt_Un
                ) {
                    // Process the result after >
                    instri++;
                    // Push this.
                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;
                    // Process.
                    instrs.Insert(instri, il.Create(OpCodes.Call, m_IsSmall));
                    instri++;
                }

                // Alternatively:

                /* We expect something similar enough to the following:
                ldfld    Celeste.AreaMode Celeste.AreaKey::Mode
                brfalse.s    // We're here

                Note that MonoMod requires the full type names (System.String instead of string)
                */
                // No need to check for the full name when the field name itself is compiler-generated.
                if (instri > 2 &&
                    instrs[instri - 1].OpCode == OpCodes.Ldfld && (instrs[instri - 1].Operand as FieldReference)?.FullName == "Celeste.AreaMode Celeste.AreaKey::Mode" &&
                    instr.OpCode == OpCodes.Brfalse_S
                ) {
                    // Process the result before !=
                    // Push this.
                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;
                    // Process.
                    instrs.Insert(instri, il.Create(OpCodes.Call, m_IsSmall));
                    instri++;
                    // Skip brfalse.s
                    instri++;
                }

            }

        }

        public static void PatchLoadLanguage(MethodDefinition method, CustomAttribute attrib) {
            // Our actual target method is the orig_ method.
            method = method.DeclaringType.FindMethod(method.GetFindableID(name: method.GetOriginalName()));

            if (!method.HasBody)
                return;

            MethodDefinition m_GetLanguageText = method.DeclaringType.FindMethod("System.Collections.Generic.IEnumerable`1<System.String> _GetLanguageText(System.String,System.Text.Encoding)");
            if (m_GetLanguageText == null)
                return;

            MethodDefinition m_ContainsKey = method.DeclaringType.FindMethod("System.Boolean _ContainsKey(System.Collections.Generic.Dictionary`2<System.String,System.String>,System.String)");
            if (m_ContainsKey == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Call && (instr.Operand as MethodReference)?.GetFindableID() == "System.Collections.Generic.IEnumerable`1<System.String> System.IO.File::ReadLines(System.String,System.Text.Encoding)") {
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_GetLanguageText;
                }

                if (instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference)?.GetFindableID() == "System.Boolean System.Collections.Generic.Dictionary`2<System.String,System.String>::ContainsKey(System.Collections.Generic.Dictionary`2<System.String,System.String>/!0)") {
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_ContainsKey;
                }
            }

        }


        public static void PatchInitLanguages(MethodDefinition method, CustomAttribute attrib) {
            // Our actual target method is the orig_ method.
            method = method.DeclaringType.FindMethod(method.GetFindableID(name: method.GetOriginalName()));

            if (!method.HasBody)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instri > 4 &&
                    instrs[instri - 3].OpCode == OpCodes.Ldc_I4_S && ((sbyte) instrs[instri - 3].Operand) == ((sbyte) '{') &&
                    instrs[instri - 2].OpCode == OpCodes.Callvirt && (instrs[instri - 2].Operand as MethodReference)?.GetFindableID() == "System.Int32 System.String::IndexOf(System.Char)" &&
                    instrs[instri - 1].OpCode == OpCodes.Ldc_I4_0 &&
                    instr.OpCode == OpCodes.Cgt) {
                    instrs[instri - 1].OpCode = OpCodes.Ldc_I4_M1;
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

            foreach (TypeDefinition type in modder.Module.Types) {
                PostProcessType(modder, type);
            }
        }

        private static void PostProcessType(MonoModder modder, TypeDefinition type) {
            foreach (MethodDefinition method in type.Methods) {
                PostProcessMethod(modder, method);
            }

            foreach (TypeDefinition nested in type.NestedTypes)
                PostProcessType(modder, nested);
        }

        private static void PostProcessMethod(MonoModder modder, MethodDefinition method) {
            // Find all FMOD-related extern methods and stub them.
            if (FMODStub &&
                !method.HasBody && method.HasPInvokeInfo && (
                method.PInvokeInfo.Module.Name == "fmod" ||
                method.PInvokeInfo.Module.Name == "fmodstudio")
            ) {
                MonoModRule.Modder.Log($"[Everest] [FMODStub] Stubbing {method.FullName} -> {method.PInvokeInfo.Module.Name}::{method.PInvokeInfo.EntryPoint}");

                if (method.HasPInvokeInfo)
                    method.PInvokeInfo = null;
                method.IsManaged = true;
                method.IsIL = true;
                method.IsNative = false;
                method.PInvokeInfo = null;
                method.IsPreserveSig = false;
                method.IsInternalCall = false;
                method.IsPInvokeImpl = false;

                MethodBody body = method.Body = new MethodBody(method);
                body.InitLocals = true;
                ILProcessor il = body.GetILProcessor();

                for (int i = 0; i < method.Parameters.Count; i++) {
                    ParameterDefinition param = method.Parameters[i];
                    if (param.IsOut || param.IsReturnValue) {
                        // il.Emit(OpCodes.Ldarg, i);
                        // il.EmitDefault(param.ParameterType, true);
                    }
                }

                il.EmitDefault(method.ReturnType ?? method.Module.TypeSystem.Void);
                il.Emit(OpCodes.Ret);
                return;
            }

        }

        public static void EmitDefault(this ILProcessor il, TypeReference t, bool stind = false, bool arrayEmpty = true) {
            if (t == null) {
                il.Emit(OpCodes.Ldnull);
                if (stind)
                    il.Emit(OpCodes.Stind_Ref);
                return;
            }

            if (t.MetadataType == MetadataType.Void)
                return;

            if (t.IsArray && arrayEmpty) {
                // TODO: Instead of emitting a null array, emit an empty array.
            }

            int var = 0;
            if (!stind) {
                var = il.Body.Variables.Count;
                il.Body.Variables.Add(new VariableDefinition(t));
                il.Emit(OpCodes.Ldloca, var);
            }
            il.Emit(OpCodes.Initobj, t);
            if (!stind)
                il.Emit(OpCodes.Ldloc, var);
        }

    }
}
