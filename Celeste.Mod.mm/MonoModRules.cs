using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MonoMod {
    /// <summary>
    /// Proxy any System.IO.File.* calls inside the method via Celeste.Mod.Helpers.FileProxy.*
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.ProxyFileCalls))]
    class ProxyFileCallsAttribute : Attribute { }

    /// <summary>
    /// Automatically fill InitMMSharedData based on the current patch flags.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchInitMMSharedData))]
    class PatchInitMMSharedDataAttribute : Attribute { }

    /// <summary>
    /// Helper for patching methods force-implemented by an interface
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchInterface))]
    class PatchInterfaceAttribute : Attribute { }

    /// <summary>
    /// Take out the "strawberry" equality check and replace it with a call to StrawberryRegistry.TrackableContains
    /// to include registered mod berries as well.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchTrackableStrawberryCheck))]
    class PatchTrackableStrawberryCheckAttribute : Attribute { }

    /// <summary>
    /// Patch references to TotalHeartGems to refer to TotalHeartGemsInVanilla instead.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchTotalHeartGemChecks))]
    class PatchTotalHeartGemChecksAttribute : Attribute { }
    /// <summary>
    /// Make the marked method the new entry point.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.MakeEntryPoint))]
    class MakeEntryPointAttribute : Attribute { }

    /// <summary>
    /// Removes the [Command] attribute from annotated method.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.RemoveCommandAttribute))]
    class RemoveCommandAttributeAttribute : Attribute { }

    /// <summary>
    /// Patches {Button,Keyboard}ConfigUI.Update (InputV2) to call a new Reset method instead of the vanilla one.
    /// Also implements mouse button remapping.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchConfigUIUpdate))]
    class PatchConfigUIUpdate : Attribute { };

    /// <summary>
    /// Forcibly changes a given member's name.
    /// </summary>
    [MonoModCustomAttribute(nameof(MonoModRules.ForceName))]
    class ForceNameAttribute : Attribute {
        public ForceNameAttribute(string name) {
        }
    }

    /// <summary>
    /// Patches the attributed method to replace _initblk calls with the initblk opcode.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchInitblk))]
    class PatchInitblkAttribute : Attribute { }

    static partial class MonoModRules {

        static bool IsCeleste;

        static Version Version;

        static IDictionary<string, MethodDefinition> FileProxyCache = new Dictionary<string, MethodDefinition>();
        static IDictionary<string, MethodDefinition> DirectoryProxyCache = new Dictionary<string, MethodDefinition>();

        static List<MethodDefinition> LevelExitRoutines = new List<MethodDefinition>();
        static List<MethodDefinition> AreaCompleteCtors = new List<MethodDefinition>();

        static MonoModRules() {
            // Note: It may actually be too late to set this to false.
            MonoModRule.Modder.MissingDependencyThrow = false;

            foreach (ModuleDefinition mod in MonoModRule.Modder.Mods)
                foreach (AssemblyNameReference dep in mod.AssemblyReferences)
                    if (dep.Name == "MonoMod" && MonoModder.Version < dep.Version)
                        throw new Exception($"Unexpected version of MonoMod patcher: {MonoModder.Version} (expected {dep.Version}+)");

            bool isFNA = false;
            bool isSteamworks = false;
            foreach (AssemblyNameReference name in MonoModRule.Modder.Module.AssemblyReferences) {
                if (name.Name.Contains("FNA"))
                    isFNA = true;
                else if (name.Name.Contains("Steamworks"))
                    isSteamworks = true;
            }
            MonoModRule.Flag.Set("FNA", isFNA);
            MonoModRule.Flag.Set("XNA", !isFNA);
            MonoModRule.Flag.Set("Steamworks", isSteamworks);
            MonoModRule.Flag.Set("NoLauncher", !isSteamworks);

            MonoModRule.Flag.Set("PatchingWithMono", Type.GetType("Mono.Runtime") != null);
            MonoModRule.Flag.Set("PatchingWithoutMono", Type.GetType("Mono.Runtime") == null);

            TypeDefinition t_Celeste = MonoModRule.Modder.FindType("Celeste.Celeste")?.Resolve();
            if (t_Celeste == null)
                return;
            IsCeleste = t_Celeste.Scope == MonoModRule.Modder.Module;

            if (IsCeleste) {
                MonoModRule.Modder.PostProcessors += PostProcessor;
            }

            // Get version - used to set any MonoMod flags.

            string versionString = null;
            int[] versionInts = null;
            // Find Celeste .ctor (luckily only has one)
            MethodDefinition c_Celeste = t_Celeste.FindMethod(".ctor", true);
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
            Version = new Version();
            if (versionString != null) {
                Version = new Version(versionString);
            }
            if (versionInts == null || versionInts.Length == 0) {
                // ???
            } else if (versionInts.Length == 2) {
                Version = new Version(versionInts[0], versionInts[1]);
            } else if (versionInts.Length == 3) {
                Version = new Version(versionInts[0], versionInts[1], versionInts[2]);
            } else if (versionInts.Length == 4) {
                Version = new Version(versionInts[0], versionInts[1], versionInts[2], versionInts[3]);
            }

            // Check if Celeste version is supported
            Version versionMin = new Version(1, 4, 0, 0);
            if (Version.Major == 0)
                Version = versionMin;
            if (Version < versionMin)
                throw new Exception($"Unsupported version of Celeste: {Version}");

            if (IsCeleste) {
                // Ensure that Celeste assembly is not already modded
                // (https://github.com/MonoMod/MonoMod#how-can-i-check-if-my-assembly-has-been-modded)
                if (MonoModRule.Modder.FindType("MonoMod.WasHere") != null)
                    throw new Exception("This version of Celeste is already modded. You need a clean install of Celeste to mod it.");

                // Ensure that FNA.dll is present / that XNA is installed.
                if (MonoModRule.Modder.FindType("Microsoft.Xna.Framework.Game")?.SafeResolve() == null)
                    throw new Exception("MonoModRules failed resolving Microsoft.Xna.Framework.Game");
            }


            // Set up flags.

            bool isWindows = PlatformHelper.Is(Platform.Windows);
            MonoModRule.Flag.Set("OS:Windows", isWindows);
            MonoModRule.Flag.Set("OS:NotWindows", !isWindows);

            MonoModRule.Flag.Set("Has:BirdTutorialGuiButtonPromptEnum", MonoModRule.Modder.FindType("Celeste.BirdTutorialGui/ButtonPrompt")?.SafeResolve() != null);
        }

        public static void ProxyFileCalls(MethodDefinition method, CustomAttribute attrib) {
            TypeDefinition t_FileProxy = MonoModRule.Modder.FindType("Celeste.Mod.Helpers.FileProxy")?.Resolve();
            TypeDefinition t_DirectoryProxy = MonoModRule.Modder.FindType("Celeste.Mod.Helpers.DirectoryProxy")?.Resolve();

            foreach (Instruction instr in method.Body.Instructions) {
                // System.IO.File.* calls are always static calls.
                if (instr.OpCode != OpCodes.Call)
                    continue;

                // We only want to replace System.IO.File.* calls.
                MethodReference calling = instr.Operand as MethodReference;
                MethodDefinition replacement;

                if (calling?.DeclaringType?.FullName == "System.IO.File") {
                    if (!FileProxyCache.TryGetValue(calling.Name, out replacement))
                        FileProxyCache[calling.GetID(withType: false)] = replacement = t_FileProxy.FindMethod(calling.GetID(withType: false));

                } else if (calling?.DeclaringType?.FullName == "System.IO.Directory") {
                    if (!DirectoryProxyCache.TryGetValue(calling.Name, out replacement))
                        DirectoryProxyCache[calling.GetID(withType: false)] = replacement = t_DirectoryProxy.FindMethod(calling.GetID(withType: false));

                } else {
                    continue;
                }

                if (replacement == null)
                    continue; // We haven't got any replacement.

                // Replace the called method with our replacement.
                instr.Operand = replacement;
            }
        }

        public static void PatchTrackableStrawberryCheck(MethodDefinition method, CustomAttribute attrib) {
            TypeDefinition t_StrawberryRegistry = MonoModRule.Modder.FindType("Celeste.Mod.StrawberryRegistry")?.Resolve();
            MethodDefinition m_TrackableContains = t_StrawberryRegistry.FindMethod("System.Boolean TrackableContains(System.String)");

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.MatchLdstr("strawberry")) {
                    instr.OpCode = OpCodes.Nop;
                    instri++;
                    instrs[instri].OpCode = OpCodes.Call;
                    instrs[instri].Operand = m_TrackableContains;
                }
            }
        }

        public static void PatchInitMMSharedData(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_Set = method.DeclaringType.FindMethod("System.Void SetMMSharedData(System.String,System.Boolean)");

            method.Body.Instructions.Clear();
            ILProcessor il = method.Body.GetILProcessor();

            foreach (KeyValuePair<string, object> kvp in MonoModRule.Modder.SharedData) {
                if (!(kvp.Value is bool))
                    return;
                il.Emit(OpCodes.Ldstr, kvp.Key);
                il.Emit((bool) kvp.Value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Call, m_Set);
            }

            il.Emit(OpCodes.Ret);
        }

        public static void PatchInterface(MethodDefinition method, CustomAttribute attrib) {
            MethodAttributes flags = MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot;
            method.Attributes |= flags;
        }

        public static void PatchTotalHeartGemChecks(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_getTotalHeartGemsInVanilla = context.Module.GetType("Celeste.SaveData").FindMethod("System.Int32 get_TotalHeartGemsInVanilla()");

            ILCursor cursor = new ILCursor(context);

            cursor.GotoNext(instr => instr.MatchCallvirt("Celeste.SaveData", "get_TotalHeartGems"));
            cursor.Next.Operand = m_getTotalHeartGemsInVanilla;
        }

        public static void MakeEntryPoint(MethodDefinition method, CustomAttribute attrib) {
            MonoModRule.Modder.Module.EntryPoint = method;
        }

        public static void RemoveCommandAttribute(MethodDefinition method, CustomAttribute attrib) {
            Mono.Collections.Generic.Collection<CustomAttribute> attributes = method.CustomAttributes;
            for (int i = attributes.Count - 1; i >= 0; i--) {
                // remove all Command attributes.
                if (attributes[i]?.AttributeType.FullName == "Monocle.Command") {
                    attributes.RemoveAt(i);
                }
            }
        }

        public static void PatchConfigUIUpdate(ILContext il, CustomAttribute attrib) {
            ILCursor c = new ILCursor(il);

            c.GotoNext(MoveType.AfterLabel, i =>
                i.MatchCallOrCallvirt("Celeste.Settings", "SetDefaultButtonControls") ||
                i.MatchCallOrCallvirt("Celeste.Settings", "SetDefaultKeyboardControls")
            );
            c.Emit(OpCodes.Pop);
            c.Emit(OpCodes.Pop);
            c.Emit(OpCodes.Ldarg_0);
            c.Next.Operand = il.Method.DeclaringType.FindMethod("System.Void Reset()");

            c.GotoNext(i => i.MatchCall("Celeste.Input", "Initialize"));
            c.Remove();

            // Add handler for Mouse Buttons on KeyboardConfigUI
            if (il.Method.DeclaringType.Name == "KeyboardConfigUI") {
                c.GotoNext(MoveType.AfterLabel, instr => instr.MatchCall("Monocle.MInput", "get_Keyboard"));
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Call, il.Method.DeclaringType.FindMethod("System.Void RemapMouse()"));
            }
        }

        public static void ForceName(ICustomAttributeProvider cap, CustomAttribute attrib) {
            if (cap is IMemberDefinition member)
                member.Name = (string) attrib.ConstructorArguments[0].Value;
        }

        
        
        public static void PatchInitblk(ILContext il, CustomAttribute attrib) {
            ILCursor c = new ILCursor(il);
            while (c.TryGotoNext(i => i.MatchCall(out MethodReference mref) && mref.Name == "_initblk")) {
                c.Next.OpCode = OpCodes.Initblk;
                c.Next.Operand = null;
            }
        }

        private static void PatchPlaySurfaceIndex(ILCursor cursor, string name) {
            MethodDefinition m_SurfaceIndex_GetPathFromIndex = cursor.Module.GetType("Celeste.SurfaceIndex").FindMethod("System.String GetPathFromIndex(System.Int32)");
            MethodReference m_String_Concat = MonoModRule.Modder.Module.ImportReference(
                MonoModRule.Modder.FindType("System.String").Resolve()
                    .FindMethod("System.String Concat(System.String,System.String)")
            );

            // Jump to ldstr we want to replace
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdstr($"event:/char/madeline{name}"));

            // Retrieve references to reuse for platform.Get___SoundIndex() call
            int loc_Platform = -1;
            MethodReference m_Platform_GetSoundIndex = null;
            cursor.FindNext(out ILCursor[] instrMatches,
                instr => instr.MatchLdloc(out loc_Platform),
                instr => (instr.MatchCallvirt(out m_Platform_GetSoundIndex) && m_Platform_GetSoundIndex.Name.EndsWith("SoundIndex")));

            /*  Change:
                    Play("event:/char/madeline{$name}", "surface_index", platformByPriority.GetStepSoundIndex(this));
                to:
                    Play(SurfaceIndex.GetPathFromIndex(platformByPriority.GetStepSoundIndex(this)) + $name, "surface_index", platformByPriority.GetStepSoundIndex(this));
                OR Change (for walls):
                    Play("event:/char/madeline/{$name}", "surface_index", platformByPriority.GetWallSoundIndex(this, (int)Facing));
                to:
                    Play(SurfaceIndex.GetPathFromIndex(platformByPriority.GetWallSoundIndex(this, (int)Facing)) + $name, "surface_index", 
                         platformByPriority.GetWallSoundIndex(this, (int)Facing));
            */

            cursor.Emit(OpCodes.Ldloc, loc_Platform);
            cursor.Emit(OpCodes.Ldarg_0);

            if (name is "/handhold" or "/grab") {
                // Player.Facing
                cursor.Emit(OpCodes.Ldarg_0);
                // If present, this will be the last argument to GetWallSoundIndex, so the instruction right before the call.
                cursor.Emit(OpCodes.Ldfld, (FieldReference) instrMatches[1].Prev.Operand);
            }

            cursor.Emit(OpCodes.Callvirt, m_Platform_GetSoundIndex);
            cursor.Emit(OpCodes.Call, m_SurfaceIndex_GetPathFromIndex);
            cursor.Emit(OpCodes.Ldstr, name);
            cursor.Emit(OpCodes.Call, m_String_Concat);
            // Remove hardcoded event string
            cursor.Remove();
        }

        /// <summary>
        /// Fix ILSpy unable to decompile enumerator after MonoMod patching<br />
        /// (<code>yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()</code>)
        /// </summary>
        public static void FixEnumeratorDecompile(TypeDefinition type) {
            foreach (MethodDefinition method in type.Methods) {
                new ILContext(method).Invoke(il => {
                    ILCursor cursor = new ILCursor(il);
                    while (cursor.TryGotoNext(instr => instr.MatchCallvirt(out MethodReference m) &&
                        (m.Name is "System.Collections.IEnumerable.GetEnumerator" or "System.IDisposable.Dispose" ||
                            m.Name.StartsWith("<>m__Finally")))
                    ) {
                        cursor.Next.OpCode = OpCodes.Call;
                    }
                });
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
            if (type.IsCompilerGeneratedEnumerator()) {
                FixEnumeratorDecompile(type);
            }
            foreach (MethodDefinition method in type.Methods) {
                method.FixShortLongOps();
            }

            foreach (TypeDefinition nested in type.NestedTypes)
                PostProcessType(modder, nested);
        }
    }
}
