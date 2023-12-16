using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace MonoMod {
    /// <summary>
    /// Automatically fill InitMMFlags based on the current patch flags.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchInitMMFlags))]
    class PatchInitMMFlags : Attribute { }

#region Game Patch Attributes
    /// <summary>
    /// Proxy any System.IO.File.* calls inside the method via Celeste.Mod.Helpers.FileProxy.*
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.ProxyFileCalls))]
    class ProxyFileCallsAttribute : Attribute { }

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
    /// Removes the [Command] attribute from annotated method.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.RemoveCommandAttribute))]
    class RemoveCommandAttributeAttribute : Attribute { }

    /// <summary>
    /// Patches {Button,Keyboard}ConfigUI.Update (InputV2) to call a new Reset method instead of the vanilla one.
    /// Also implements mouse button remapping.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchConfigUIUpdate))]
    class PatchConfigUIUpdate : Attribute { }

    /// <summary>
    /// Swaps BlendFunction.Min/Max for BlendState construction, as they are switched on XNA/FNA
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchMinMaxBlendFunction))]
    class PatchMinMaxBlendFunction : Attribute {}
#endregion

    static partial class MonoModRules {

        public static Version MinimumGameVersion = new Version(1, 4, 0, 0);
        public static bool IsRelinkingXNAInstall { get; private set; }

        static List<MethodDefinition> LevelExitRoutines = new List<MethodDefinition>();
        static List<MethodDefinition> AreaCompleteCtors = new List<MethodDefinition>();

        // Init rules for patching Celeste.exe
        private static void InitGameRules(MonoModder modder) {
            // Ensure that Celeste assembly is not already modded
            // (https://github.com/MonoMod/MonoMod#how-can-i-check-if-my-assembly-has-been-modded)
            if (modder.FindType("MonoMod.WasHere")?.Scope == modder.Module)
                throw new Exception("This version of Celeste is already modded. You need a clean install of Celeste to mod it.");

            // Check if Celeste version is supported
            Version gameVer = DetermineGameVersion(modder);
            if (gameVer < MinimumGameVersion)
                throw new Exception($"Unsupported version of Celeste: {gameVer}");

            // Determine if this is a Steam build
            bool isSteamworks = modder.Module.AssemblyReferences.Any(a => a.Name.Contains("Steamworks"));

            // Set up game flags
            // These will be passed onto mods through InitMMFlags
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            MonoModRule.Flag.Set("OS:Windows", isWindows);
            MonoModRule.Flag.Set("OS:NotWindows", !isWindows);

            MonoModRule.Flag.Set("FNA", true); //Keep FNA/XNA flags around for legacy code
            MonoModRule.Flag.Set("XNA", false);
            MonoModRule.Flag.Set("Steamworks", isSteamworks);
            MonoModRule.Flag.Set("NoLauncher", !isSteamworks);

            MonoModRule.Flag.Set("Has:BirdTutorialGuiButtonPromptEnum", MonoModRule.Modder.FindType("Celeste.BirdTutorialGui/ButtonPrompt")?.SafeResolve() != null);

            // Run game preprocessor
            GamePreProcessor(modder);

            // Add game post processor
            modder.PostProcessors += GamePostProcessor;

            // Remove patches targeting game dependencies
            RemoveDependencyPatches();
        }

        private static Version DetermineGameVersion(MonoModder modder) {
            // Find Celeste .ctor (luckily only has one)
            MethodDefinition celesteCtor = modder.FindType("Celeste.Celeste").Resolve().FindMethod(".ctor", true);

            if (celesteCtor == null || !celesteCtor.HasBody)
                throw new InvalidOperationException("Couldn't find Celeste.Celeste constructor");

            // Analyze the constructor body
            Collection<Instruction> instrs = celesteCtor.Body.Instructions;
            for (int instrIdx = 0; instrIdx < instrs.Count; instrIdx++) {
                Instruction instr = celesteCtor.Body.Instructions[instrIdx];

                // Is this creating a new System.Version instance?
                MethodReference verCtor = instr.Operand as MethodReference;
                if (instr.OpCode != OpCodes.Newobj || verCtor?.DeclaringType?.FullName != "System.Version")
                    continue;

                // Check if all parameters are of type int.
                if (verCtor.Parameters.All(p => p.ParameterType.MetadataType == MetadataType.Int32)) {
                    // Assume that ldc.i4* instructions are right before the newobj.
                    int[] verInts = new int[verCtor.Parameters.Count];
                    for (int i = 0; i < verInts.Length; i++)
                        verInts[i] = instrs[instrIdx - verInts.Length + i].GetInt();

                    switch(verInts.Length) {
                        case 2: return new Version(verInts[0], verInts[1]);
                        case 3: return new Version(verInts[0], verInts[1], verInts[2]);
                        case 4: return new Version(verInts[0], verInts[1], verInts[2], verInts[3]);
                    }
                }

                // Check if the constructor only takes a single string
                if (verCtor.Parameters.Count == 1 && verCtor.Parameters[0].ParameterType.MetadataType == MetadataType.String) {
                    // Assume that a ldstr is right before the newobj.
                    string verString = instrs[instrIdx - 1].Operand as string;
                    return new Version(verString);
                }
            }

            throw new InvalidOperationException("Couldn't determine Celeste version");
        }

        public static void GamePreProcessor(MonoModder modder) {
            // Relink against FNA
            IsRelinkingXNAInstall = RelinkAgainstFNA(modder);
            MonoModRule.Flag.Set("RelinkXNA", IsRelinkingXNAInstall);
            MonoModRule.Flag.Set("DontRelinkXNA", !IsRelinkingXNAInstall);

            if (IsRelinkingXNAInstall)
                modder.Log("[Celeste.Mod.mm] Converting XNA game install to FNA");

            static void VisitType(TypeDefinition type) {
                // Remove readonly attribute from all static fields
                // This "fixes" https://github.com/dotnet/runtime/issues/11571, which breaks some mods
                foreach (FieldDefinition field in type.Fields)
                    if ((field.Attributes & FieldAttributes.Static) != 0)
                        field.Attributes &= ~FieldAttributes.InitOnly;

                // Visit nested types
                foreach (TypeDefinition nestedType in type.NestedTypes)
                    VisitType(nestedType);
            }

            foreach (TypeDefinition type in modder.Module.Types)
                VisitType(type);
        }

        public static void GamePostProcessor(MonoModder modder) {
            // Patch previously registered AreaCompleteCtors and LevelExitRoutines _in that order._
            foreach (MethodDefinition method in AreaCompleteCtors)
                PatchAreaCompleteCtor(method);
            foreach (MethodDefinition method in LevelExitRoutines)
                PatchLevelExitRoutine(method);
        }

        public static void PatchInitMMFlags(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_Set = method.DeclaringType.FindMethod("System.Void SetMMFlag(MonoMod.MonoModder,System.String,System.Boolean)");

            method.Body.Instructions.Clear();
            ILProcessor il = method.Body.GetILProcessor();

            // Set the same flags which currently set
            foreach (KeyValuePair<string, object> kvp in MonoModRule.Modder.SharedData) {
                if (!(kvp.Value is bool))
                    return;

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldstr, kvp.Key);
                il.Emit((bool) kvp.Value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Call, m_Set);
            }

            il.Emit(OpCodes.Ret);
        }

#region Game Patches
        private static IDictionary<string, MethodDefinition> _FileProxyCache = new Dictionary<string, MethodDefinition>();
        private static IDictionary<string, MethodDefinition> _DirectoryProxyCache = new Dictionary<string, MethodDefinition>();
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
                    if (!_FileProxyCache.TryGetValue(calling.Name, out replacement))
                        _FileProxyCache[calling.GetID(withType: false)] = replacement = t_FileProxy.FindMethod(calling.GetID(withType: false));

                } else if (calling?.DeclaringType?.FullName == "System.IO.Directory") {
                    if (!_DirectoryProxyCache.TryGetValue(calling.Name, out replacement))
                        _DirectoryProxyCache[calling.GetID(withType: false)] = replacement = t_DirectoryProxy.FindMethod(calling.GetID(withType: false));

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

        public static void PatchTotalHeartGemChecks(ILContext context, CustomAttribute attrib) {
            MethodDefinition m_getTotalHeartGemsInVanilla = context.Module.GetType("Celeste.SaveData").FindMethod("System.Int32 get_TotalHeartGemsInVanilla()");

            ILCursor cursor = new ILCursor(context);

            cursor.GotoNext(instr => instr.MatchCallvirt("Celeste.SaveData", "get_TotalHeartGems"));
            cursor.Next.Operand = m_getTotalHeartGemsInVanilla;
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

        public static void PatchMinMaxBlendFunction(ILContext il, CustomAttribute attrib) {
            ILCursor c = new ILCursor(il);
            while (c.TryGotoNext(MoveType.After, i =>
                i.MatchCallOrCallvirt("Microsoft.Xna.Framework.Graphics.BlendState", "set_ColorBlendFunction") ||
                i.MatchCallOrCallvirt("Microsoft.Xna.Framework.Graphics.BlendState", "set_AlphaBlendFunction")
            )) {
                // Instruction right before the call must be a ldc.i4.XYZ
                if (!c.Instrs[c.Index-2].MatchLdcI4(out int val))
                    throw new Exception("Unexpected instruction before call to ColorBlendFunction/AlphaBlendFunction setter");

                // Swap Min/Max blend functions
                if (val == (int) BlendFunction.Min)
                    c.Instrs[c.Index-2] = Instruction.Create(OpCodes.Ldc_I4, (int) BlendFunction.Max);
                else if (val == (int) BlendFunction.Max)
                    c.Instrs[c.Index-2] = Instruction.Create(OpCodes.Ldc_I4, (int) BlendFunction.Min);
            }
        }
        #endregion

    }
}
