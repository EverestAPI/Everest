using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoMod {
    /// <summary>
    /// Proxy any System.IO.File.* calls inside the method via Celeste.Mod.Helpers.FileProxy.*
    /// </summary>
    [MonoModCustomMethodAttribute("ProxyFileCalls")]
    class ProxyFileCallsAttribute : Attribute { }

    /// <summary>
    /// Check for ldstr "Corrupted Level Data" and pop the throw after that.
    /// Also manually execute ProxyFileCalls rule.
    /// Also includes a patch for the strawberry tracker.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchMapDataLoader")]
    class PatchMapDataLoaderAttribute : Attribute { }

    /// <summary>
    /// A patch for the strawberry tracker, allowing all registered modded berries to be detected.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchLevelDataBerryTracker")]
    class PatchLevelDataBerryTracker : Attribute { }

    /// <summary>
    /// Patch the Godzilla-sized level loading method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchLevelLoader")]
    class PatchLevelLoaderAttribute : Attribute { }

    /// <summary>
    /// A patch for Strawberry that takes into account that some modded strawberries may not allow standard collection rules.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchStrawberryTrainCollectionOrder")]
    class PatchStrawberryTrainCollectionOrder : Attribute { }

    /// <summary>
    /// Patch the Godzilla-sized backdrop parsing method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchBackdropParser")]
    class PatchBackdropParserAttribute : Attribute { }

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
    /// Patch the Godzilla-sized level transition method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchTransitionRoutine")]
    class PatchTransitionRoutineAttribute : Attribute { }

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
    /// Patch the Badeline boss OnPlayer method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchBadelineBossOnPlayer")]
    class PatchBadelineBossOnPlayerAttribute : Attribute { }

    /// <summary>
    /// Patch the Cloud.Added method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchCloudAdded")]
    class PatchCloudAddedAttribute : Attribute { }

    /// <summary>
    /// Patch the RainFG.Render method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchRainFGRender")]
    class PatchRainFGRenderAttribute : Attribute { }

    /// <summary>
    /// Patch the Dialog.Load method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchDialogLoader")]
    class PatchDialogLoaderAttribute : Attribute { }

    /// <summary>
    /// Patch the Language.LoadTxt method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchLoadLanguage")]
    class PatchLoadLanguageAttribute : Attribute { }

    /// <summary>
    /// Automatically fill InitMMSharedData based on the current patch flags.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchInitMMSharedData")]
    class PatchInitMMSharedDataAttribute : Attribute { }

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

    /// <summary>
    /// Patch the GameLoader.IntroRoutine method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchGameLoaderIntroRoutine")]
    class PatchGameLoaderIntroRoutineAttribute : Attribute { }

    /// <summary>
    /// Patch the UserIO.SaveRoutine method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchSaveRoutine")]
    class PatchSaveRoutineAttribute : Attribute { }

    /// <summary>
    /// Patch the orig_Update method in Player instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchPlayerOrigUpdate")]
    class PatchPlayerOrigUpdateAttribute : Attribute { }

    /// <summary>
    /// Patch the SwapRoutine method in OuiChapterPanel instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchChapterPanelSwapRoutine")]
    class PatchChapterPanelSwapRoutineAttribute : Attribute { }

    /// <summary>
    /// Patch the Strawberry class to tack on the IStrawberry interface for the StrawberryRegistry
    /// </summary>
    [MonoModCustomAttribute("PatchStrawberryInterface")]
    class PatchStrawberryInterfaceAttribute : Attribute { }

    /// <summary>
    /// Helper for patching methods force-implemented by an interface
    /// </summary>
    [MonoModCustomMethodAttribute("PatchInterface")]
    class PatchInterfaceAttribute : Attribute { };

    /// <summary>
    /// IL-patch the Render method for file select slots instead of reimplementing it,
    /// to un-hardcode stamps.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchFileSelectSlotRender")]
    class PatchFileSelectSlotRenderAttribute : Attribute { };

    /// <summary>
    /// Take out the "strawberry" equality check and replace it with a call to StrawberryRegistry.TrackableContains
    /// to include registered mod berries as well.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchTrackableStrawberryCheck")]
    class PatchTrackableStrawberryCheckAttribute : Attribute { };

    /// <summary>
    /// Patch the pathfinder debug rendering to make it aware of the array size being unhardcoded.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchPathfinderRender")]
    class PatchPathfinderRenderAttribute : Attribute { };

    /// <summary>
    /// Patch references to TotalHeartGems to refer to TotalHeartGemsInVanilla instead.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchTotalHeartGemChecks")]
    class PatchTotalHeartGemChecksAttribute : Attribute { };

    /// <summary>
    /// Same as above, but for references in routines.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchTotalHeartGemChecksInRoutine")]
    class PatchTotalHeartGemChecksInRoutineAttribute : Attribute { };

    /// <summary>
    /// Patch a reference to TotalHeartGems in the OuiJournalGlobal constructor to unharcode the check for golden berry unlock.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchOuiJournalStatsHeartGemCheck")]
    class PatchOuiJournalStatsHeartGemCheckAttribute : Attribute { };

    /// <summary>
    /// Makes the annotated method public.
    /// </summary>
    [MonoModCustomMethodAttribute("MakeMethodPublic")]
    class MakeMethodPublicAttribute : Attribute { };

    /// <summary>
    /// Patches the CrystalStaticSpinner.AddSprites method to make it more efficient.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchSpinnerCreateSprites")]
    class PatchSpinnerCreateSpritesAttribute : Attribute { };

    /// <summary>
    /// Patches the checks for OuiAssistMode to include a check for OuiFileSelectSlot.ISubmenu as well.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchOuiFileSelectSubmenuChecks")]
    class PatchOuiFileSelectSubmenuChecksAttribute : Attribute { };

    /// <summary>
    /// Patches the Fonts.Prepare method to also include custom fonts.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchFontsPrepare")]
    class PatchFontsPrepareAttribute : Attribute { };

    /// <summary>
    /// Make the marked method the new entry point.
    /// </summary>
    [MonoModCustomMethodAttribute("MakeEntryPoint")]
    class MakeEntryPointAttribute : Attribute { };

    /// <summary>
    /// Patch the original Celeste entry point instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchCelesteMain")]
    class PatchCelesteMainAttribute : Attribute { };

    /// <summary>
    /// Removes the [Command] attribute from the matching vanilla method in Celeste.Commands.
    /// </summary>
    [MonoModCustomMethodAttribute("RemoveCommandAttributeFromVanillaLoadMethod")]
    class RemoveCommandAttributeFromVanillaLoadMethodAttribute : Attribute { };

    /// <summary>
    /// Patch the fake heart color to make it customizable.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchFakeHeartColor")]
    class PatchFakeHeartColorAttribute : Attribute { };

    /// <summary>
    /// Patch the file naming rendering to hide the "switch between katakana and hiragana" prompt when the menu is not focused.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchOuiFileNamingRendering")]
    class PatchOuiFileNamingRenderingAttribute : Attribute { };

    /// <summary>
    /// Include the option to use Y range of trigger nodes.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchRumbleTriggerAwake")]
    class PatchRumbleTriggerAwakeAttribute : Attribute { };

    /// <summary>
    /// Include check for custom events.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchEventTriggerOnEnter")]
    class PatchEventTriggerOnEnterAttribute : Attribute { };

    /// <summary>
    /// Modify collision to make it customizable.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchWaterUpdate")]
    class PatchWaterUpdateAttribute : Attribute { };

    /// <summary>
    /// Add custom dialog to fake hearts.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchFakeHeartDialog")]
    class PatchFakeHeartDialogAttribute : Attribute { };

    /// <summary>
    /// Include checks for manual triggering.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchIntroCrusherSequence")]
    class PatchIntroCrusherSequenceAttribute : Attribute { };

    /// <summary>
    /// Patches the unselected color in TextMenu.Option to make it customizable.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchTextMenuOptionColor")]
    class PatchTextMenuOptionColorAttribute : Attribute { };

    /// <summary>
    /// Patches chapter panel rendering to allow for custom chapter cards.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchOuiChapterPanelRender")]
    class PatchOuiChapterPanelRenderAttribute : Attribute { };

    /// <summary>
    /// Patches GoldenBlocks to disable static movers if the block is disabled.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchGoldenBlockStaticMovers")]
    class PatchGoldenBlockStaticMoversAttribute : Attribute { };

    /// <summary>
    /// Don't remove TalkComponent even watchtower collide solid, so that watchtower can be hidden behind Solid.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchLookoutUpdate")]
    class PatchLookoutUpdateAttribute : Attribute { };

    /// <summary>
    /// Un-hardcode the range of the "Scared" decals.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchDecalUpdate")]
    class PatchDecalUpdateAttribute : Attribute { };

    /// <summary>
    /// Patches LevelExit.Begin to make the endscreen music customizable.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchAreaCompleteMusic")]
    class PatchAreaCompleteMusicAttribute : Attribute { };

    /// <summary>
    /// Patches AreaComplete.VersionNumberAndVariants to offset the version number when necessary.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchAreaCompleteVersionNumberAndVariants")]
    class PatchAreaCompleteVersionNumberAndVariantsAttribute : Attribute { };

    /// <summary>
    /// Patches {Button,Keyboard}ConfigUI.Update (InputV2) to call a new Reset method instead of the vanilla one.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchInputConfigReset")]
    class PatchInputConfigResetAttribute : Attribute { };

    /// <summary>
    /// Patches AscendManager.Routine to fix gameplay RNG in custom maps.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchAscendManagerRoutine")]
    class PatchAscendManagerRoutineAttribute : Attribute { }

    /// <summary>
    /// Patches Commands.UpdateOpen to make key's repeat timer independent with time rate.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchCommandsUpdateOpen")]
    class PatchCommandsUpdateOpenAttribute : Attribute { }

    /// <summary>
    /// Forcibly changes a given member's name.
    /// </summary>
    [MonoModCustomAttribute("ForceName")]
    class ForceNameAttribute : Attribute {
        public ForceNameAttribute(string name) {
        }
    };

    /// <summary>
    /// Patches OuiFileSelect.Enter to fix file slots missing bug.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchOuiFileSelectEnter")]
    class PatchOuiFileSelectEnterAttribute : Attribute { }

    /// <summary>
    /// Patches OuiFileSelect.LoadThread to fix file slots missing bug.
    /// </summary>
    [MonoModCustomMethodAttribute("PatchOuiFileSelectLoadThread")]
    class PatchOuiFileSelectLoadThreadAttribute : Attribute { }

    /// <summary>
    /// Replaces typeof(CoroutineDelayHackfixHelper) with typeof(compiler-generated method associated to CoroutineDelayHackfixHelper.Wrap).
    /// </summary>
    [MonoModCustomMethodAttribute("PatchCoroutineHackfixHelperStateMachine")]
    class PatchCoroutineHackfixHelperStateMachineAttribute : Attribute { }

    static class MonoModRules {

        static bool IsCeleste;

        static bool FMODStub = false;

        static TypeDefinition Celeste;

        static TypeDefinition Everest;
        static MethodDefinition m_Everest_get_VersionCelesteString;

        static TypeDefinition Level;

        static TypeDefinition StrawberryRegistry;
        static InterfaceImplementation IStrawberry;

        static TypeDefinition SaveData;

        static TypeDefinition FileProxy;
        static TypeDefinition DirectoryProxy;
        static IDictionary<string, MethodDefinition> FileProxyCache = new Dictionary<string, MethodDefinition>();
        static IDictionary<string, MethodDefinition> DirectoryProxyCache = new Dictionary<string, MethodDefinition>();

        static List<MethodDefinition> LevelExitRoutines = new List<MethodDefinition>();
        static List<MethodDefinition> AreaCompleteCtors = new List<MethodDefinition>();

        static MonoModRules() {
            // Note: It may actually be too late to set this to false.
            MonoModRule.Modder.MissingDependencyThrow = false;

            FMODStub = Environment.GetEnvironmentVariable("EVEREST_FMOD_STUB") == "1";
            MonoModRule.Flag.Set("FMODStub", FMODStub);

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
            }
            if (versionInts == null || versionInts.Length == 0) {
                // ???
            } else if (versionInts.Length == 2) {
                version = new Version(versionInts[0], versionInts[1]);
            } else if (versionInts.Length == 3) {
                version = new Version(versionInts[0], versionInts[1], versionInts[2]);
            } else if (versionInts.Length == 4) {
                version = new Version(versionInts[0], versionInts[1], versionInts[2], versionInts[3]);
            }

            // Check if Celeste version is supported
            Version versionMin = new Version(1, 3, 1, 2);
            if (version.Major == 0)
                version = versionMin;
            if (version < versionMin)
                throw new Exception($"Unsupported version of Celeste: {version}");

            if (IsCeleste) {
                // Ensure that Celeste assembly is not already modded
                // (https://github.com/MonoMod/MonoMod#how-can-i-check-if-my-assembly-has-been-modded)
                if (MonoModRule.Modder.FindType("MonoMod.WasHere") != null)
                    throw new Exception("This version of Celeste is already modded. You need a clean install of Celeste to mod it.");
            }


            // Set up flags.

            bool isWindows = PlatformHelper.Is(Platform.Windows);
            MonoModRule.Flag.Set("OS:Windows", isWindows);
            MonoModRule.Flag.Set("OS:NotWindows", !isWindows);

            MonoModRule.Flag.Set("Has:BirdTutorialGuiButtonPromptEnum", MonoModRule.Modder.FindType("Celeste.BirdTutorialGui/ButtonPrompt")?.SafeResolve() != null);
            MonoModRule.Flag.Set("Fill:CompleteRendererImageLayerScale", MonoModRule.Modder.FindType("Celeste.CompleteRenderer/ImageLayer").Resolve().FindField("Scale") == null);

            TypeDefinition t_Input = MonoModRule.Modder.FindType("Celeste.Input").Resolve();
            MethodDefinition m_GuiInputController = t_Input.FindMethod("GuiInputController");
            MonoModRule.Flag.Set("V1:GuiInputController", m_GuiInputController.Parameters.Count == 0);
            MonoModRule.Flag.Set("V2:GuiInputController", m_GuiInputController.Parameters.Count == 1);

            MonoModRule.Flag.Set("V1:Input", MonoModRule.Modder.FindType("Celeste.Settings").Resolve().FindField("BtnJump") != null);
            MonoModRule.Flag.Set("V2:Input", MonoModRule.Modder.FindType("Celeste.Settings").Resolve().FindField("BtnJump") == null);

            MonoModRule.Flag.Set("V1:SubHeader", MonoModRule.Modder.FindType("Celeste.TextMenu/SubHeader").Resolve().FindMethod("System.Void .ctor(System.String)") != null);
            MonoModRule.Flag.Set("V2:SubHeader", MonoModRule.Modder.FindType("Celeste.TextMenu/SubHeader").Resolve().FindMethod("System.Void .ctor(System.String,System.Boolean)") != null);

            MonoModRule.Flag.Set("V1:TrySquishWiggle", MonoModRule.Modder.FindType("Celeste.Actor").Resolve().FindMethod("System.Boolean TrySquishWiggle(Celeste.CollisionData)") != null);
            MonoModRule.Flag.Set("V2:TrySquishWiggle", MonoModRule.Modder.FindType("Celeste.Actor").Resolve().FindMethod("System.Boolean TrySquishWiggle(Celeste.CollisionData,System.Int32,System.Int32)") != null);
        }

        public static void ProxyFileCalls(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            if (FileProxy == null)
                FileProxy = MonoModRule.Modder.FindType("Celeste.Mod.Helpers.FileProxy")?.Resolve();
            if (FileProxy == null)
                return;

            if (DirectoryProxy == null)
                DirectoryProxy = MonoModRule.Modder.FindType("Celeste.Mod.Helpers.DirectoryProxy")?.Resolve();
            if (DirectoryProxy == null)
                return;

            foreach (Instruction instr in method.Body.Instructions) {
                // System.IO.File.* calls are always static calls.
                if (instr.OpCode != OpCodes.Call)
                    continue;

                // We only want to replace System.IO.File.* calls.
                MethodReference calling = instr.Operand as MethodReference;
                MethodDefinition replacement;

                if (calling?.DeclaringType?.FullName == "System.IO.File") {
                    if (!FileProxyCache.TryGetValue(calling.Name, out replacement))
                        FileProxyCache[calling.GetID(withType: false)] = replacement = FileProxy.FindMethod(calling.GetID(withType: false));

                } else if (calling?.DeclaringType?.FullName == "System.IO.Directory") {
                    if (!DirectoryProxyCache.TryGetValue(calling.Name, out replacement))
                        DirectoryProxyCache[calling.GetID(withType: false)] = replacement = DirectoryProxy.FindMethod(calling.GetID(withType: false));

                } else {
                    continue;
                }

                if (replacement == null)
                    continue; // We haven't got any replacement.

                // Replace the called method with our replacement.
                instr.Operand = replacement;
            }

        }

        public static void PatchMapDataLoader(MethodDefinition method, CustomAttribute attrib) {
            // Our actual target method is the orig_ method.
            method = method.DeclaringType.FindMethod(method.GetID(name: method.GetOriginalName()));

            if (!method.HasBody)
                return;

            ProxyFileCalls(method, attrib);

            MethodDefinition m_Process = method.DeclaringType.FindMethod("Celeste.BinaryPacker/Element _Process(Celeste.BinaryPacker/Element,Celeste.MapData)");
            if (m_Process == null)
                return;

            MethodDefinition m_GrowAndGet = method.DeclaringType.FindMethod("Celeste.EntityData _GrowAndGet(Celeste.EntityData[0...,0...]&,System.Int32,System.Int32)");
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

                if (instr.OpCode == OpCodes.Call && (instr.Operand as MethodReference)?.GetID() == "Celeste.BinaryPacker/Element Celeste.BinaryPacker::FromBinary(System.String)") {
                    instri++;

                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;
                    instrs.Insert(instri, il.Create(OpCodes.Call, m_Process));
                    instri++;
                }

                if (instri > 2 &&
                    instrs[instri - 3].OpCode == OpCodes.Ldfld && (instrs[instri - 3].Operand as FieldReference)?.FullName == "Celeste.EntityData[0...,0...] Celeste.ModeProperties::StrawberriesByCheckpoint" &&
                    instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference)?.GetID() == "Celeste.EntityData Celeste.EntityData[0...,0...]::Get(System.Int32,System.Int32)"
                ) {
                    instrs[instri - 3].OpCode = OpCodes.Ldflda;
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_GrowAndGet;
                    instri++;
                }
            }

        }

        public static void PatchTrackableStrawberryCheck(MethodDefinition method, CustomAttribute attrib) {
            if (StrawberryRegistry == null)
                StrawberryRegistry = MonoModRule.Modder.FindType("Celeste.Mod.StrawberryRegistry")?.Resolve();
            if (StrawberryRegistry == null)
                return;

            MethodDefinition m_TrackableContains = StrawberryRegistry.FindMethod("System.Boolean TrackableContains(System.String)");
            if (m_TrackableContains == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;

            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Ldstr && (instr.Operand as string) == "strawberry") {
                    instr.OpCode = OpCodes.Nop;
                    instrs[instri + 1].OpCode = OpCodes.Call;
                    instrs[instri + 1].Operand = m_TrackableContains;
                    instri++;
                }
            }
        }

        public static void PatchLevelDataBerryTracker(MethodDefinition method, CustomAttribute attrib) {
            // Our actual target method is the orig_ method.
            method = method.DeclaringType.FindMethod(method.GetID(name: method.GetOriginalName()));

            if (!method.HasBody)
                return;

            if (StrawberryRegistry == null)
                StrawberryRegistry = MonoModRule.Modder.FindType("Celeste.Mod.StrawberryRegistry")?.Resolve();
            if (StrawberryRegistry == null)
                return;

            MethodDefinition m_TrackableContains = StrawberryRegistry.FindMethod("System.Boolean TrackableContains(Celeste.BinaryPacker/Element)");
            if (m_TrackableContains == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                /*
                   we found

                   IL_08BA: ldloc.s   V_14
                   IL_08BC: ldfld     string Celeste.BinaryPacker/Element::Name
                   IL_08C1: ldstr     "strawberry"      <-- YOU ARE HERE
                   IL_08C6: call      bool [mscorlib]System.String::op_Equality(string, string)
                   IL_08CB: brtrue.s  IL_08E0
                */

                // Strawberry tracker adjustments
                if (instr.OpCode == OpCodes.Ldstr && (instr.Operand as string) == "strawberry") {

                    instr.OpCode = OpCodes.Nop;
                    instrs[instri - 1].OpCode = OpCodes.Nop;
                    instrs[instri + 1].Operand = m_TrackableContains;
                    instri++;
                }
            }
        }

        public static void PatchStrawberryTrainCollectionOrder(MethodDefinition method, CustomAttribute attrib) {
            // Our actual target method is the orig_ method.
            method = method.DeclaringType.FindMethod(method.GetID(name: method.GetOriginalName()));

            if (!method.HasBody)
                return;

            if (StrawberryRegistry == null)
                StrawberryRegistry = MonoModRule.Modder.FindType("Celeste.Mod.StrawberryRegistry")?.Resolve();
            if (StrawberryRegistry == null)
                return;

            MethodDefinition m_IsFirst = StrawberryRegistry.FindMethod("System.Boolean IsFirstStrawberry(Monocle.Entity)");
            if (m_IsFirst == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                // Rip out the vanilla code call and replace it with vanilla-considerate code
                if (instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference)?.GetID().Contains("IsFirstStrawberry") == true) {
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_IsFirst;
                    instri++;
                }
            }
        }

        public static void PatchLevelLoader(MethodDefinition method, CustomAttribute attrib) {
            // Our actual target method is the orig_ method.
            method = method.DeclaringType.FindMethod(method.GetID(name: method.GetOriginalName()));

            if (!method.HasBody)
                return;

            MethodDefinition m_LoadNewPlayer = method.DeclaringType.FindMethod("Celeste.Player LoadNewPlayer(Microsoft.Xna.Framework.Vector2,Celeste.PlayerSpriteMode)");
            if (m_LoadNewPlayer == null)
                return;

            MethodDefinition m_LoadCustomEntity = method.DeclaringType.FindMethod("System.Boolean LoadCustomEntity(Celeste.EntityData,Celeste.Level)");
            if (m_LoadCustomEntity == null)
                return;

            // We also need to do special work in the cctor.
            MethodDefinition m_cctor = method.DeclaringType.FindMethod(".cctor");
            if (m_cctor == null)
                return;

            FieldDefinition f_LoadStrings = method.DeclaringType.FindField("_LoadStrings");
            if (f_LoadStrings == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> cctor_instrs = m_cctor.Body.Instructions;
            ILProcessor cctor_il = m_cctor.Body.GetILProcessor();

            // Remove cctor ret for simplicity. Re-add later.
            cctor_instrs.RemoveAt(cctor_instrs.Count - 1);

            TypeDefinition td_LoadStrings = f_LoadStrings.FieldType.Resolve();
            MethodReference m_LoadStrings_Add = MonoModRule.Modder.Module.ImportReference(td_LoadStrings.FindMethod("Add"));
            m_LoadStrings_Add.DeclaringType = f_LoadStrings.FieldType;
            MethodReference m_LoadStrings_ctor = MonoModRule.Modder.Module.ImportReference(td_LoadStrings.FindMethod("System.Void .ctor()"));
            m_LoadStrings_ctor.DeclaringType = f_LoadStrings.FieldType;
            cctor_il.Emit(OpCodes.Newobj, m_LoadStrings_ctor);

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Newobj && (instr.Operand as MethodReference)?.GetID() == "System.Void Celeste.Player::.ctor(Microsoft.Xna.Framework.Vector2,Celeste.PlayerSpriteMode)") {
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_LoadNewPlayer;
                }

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
                        instrs[instri + 3].OpCode == OpCodes.Call && (instrs[instri + 3].Operand as MethodReference)?.GetID() == "System.UInt32 <PrivateImplementationDetails>::ComputeStringHash(System.String)"
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

                if (instr.OpCode == OpCodes.Ldstr) {
                    cctor_il.Emit(OpCodes.Dup);
                    cctor_il.Emit(OpCodes.Ldstr, instr.Operand);
                    cctor_il.Emit(OpCodes.Callvirt, m_LoadStrings_Add);
                    cctor_il.Emit(OpCodes.Pop); // HashSet.Add returns a bool.
                }

            }

            cctor_il.Emit(OpCodes.Stsfld, f_LoadStrings);
            cctor_il.Emit(OpCodes.Ret);
        }

        public static void PatchBackdropParser(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            MethodDefinition m_LoadCustomBackdrop = method.DeclaringType.FindMethod("Celeste.Backdrop LoadCustomBackdrop(Celeste.BinaryPacker/Element,Celeste.BinaryPacker/Element,Celeste.MapData)");
            if (m_LoadCustomBackdrop == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();

            // Load custom backdrop at the beginning of the method.
            // If it's been loaded, skip to backdrop setup.

            Instruction origStart = instrs[0];

            il.InsertBefore(origStart, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(origStart, il.Create(OpCodes.Ldarg_2));
            il.InsertBefore(origStart, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(origStart, il.Create(OpCodes.Call, m_LoadCustomBackdrop));
            il.InsertBefore(origStart, il.Create(OpCodes.Dup));
            il.InsertBefore(origStart, il.Create(OpCodes.Stloc_0));

            Instruction branchCustomToSetup = il.Create(OpCodes.Nop);
            branchCustomToSetup.OpCode = OpCodes.Brtrue;
            branchCustomToSetup.Operand = null;
            il.InsertBefore(origStart, branchCustomToSetup);

            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode != OpCodes.Ldstr || instr.Operand as string != "tag")
                    continue;

                branchCustomToSetup.Operand = instr.Previous;
                break;
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
                    instrs[instri - 2].OpCode == OpCodes.Call && (instrs[instri - 2].Operand as MethodReference)?.GetID() == "Monocle.MInput/KeyboardData Monocle.MInput::get_Keyboard()" &&
                    instrs[instri - 1].GetIntOrNull() == 9 &&
                    instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference)?.GetID() == "System.Boolean Monocle.MInput/KeyboardData::Pressed(Microsoft.Xna.Framework.Input.Keys)"
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
                    instrs[instri + 2].OpCode == OpCodes.Callvirt && (instrs[instri + 2].Operand as MethodReference)?.GetID() == "System.Void Monocle.Renderer::Render(Monocle.Scene)"
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
                    instr.OpCode == OpCodes.Newobj && (instr.Operand as MethodReference)?.GetID() == "System.Void Celeste.HudRenderer::.ctor()" &&
                    instrs[instri + 1].OpCode == OpCodes.Dup &&
                    instrs[instri + 2].OpCode.Name.ToLowerInvariant().StartsWith("stloc") &&
                    instrs[instri + 3].OpCode == OpCodes.Stfld && (instrs[instri + 3].Operand as FieldReference)?.FullName == "Celeste.HudRenderer Celeste.Level::HudRenderer" &&
                    instrs[instri + 4].OpCode.Name.ToLowerInvariant().StartsWith("ldloc") &&
                    instrs[instri + 5].OpCode == OpCodes.Callvirt && (instrs[instri + 5].Operand as MethodReference)?.GetID() == "System.Void Monocle.Scene::Add(Monocle.Renderer)"
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

        public static void PatchTransitionRoutine(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_GCCollect = method.DeclaringType.FindMethod("System.Void _GCCollect()");
            if (m_GCCollect == null)
                return;

            // The gem collection routine is stored in a compiler-generated method.
            foreach (TypeDefinition nest in method.DeclaringType.NestedTypes) {
                if (!nest.Name.StartsWith("<" + method.Name + ">d__"))
                    continue;
                method = nest.FindMethod("System.Boolean MoveNext()") ?? method;
                break;
            }

            if (!method.HasBody)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Call && (instr.Operand as MethodReference)?.GetID() == "System.Void System.GC::Collect()") {
                    // Replace the method call.
                    instr.Operand = m_GCCollect;
                    instri++;
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
                    instrs[instri + 1].OpCode == OpCodes.Callvirt && (instrs[instri + 1].Operand as MethodReference)?.GetID() == "System.String System.Object::ToString()"
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
            // Our actual target method is the orig_ method.
            method = method.DeclaringType.FindMethod(method.GetID(name: method.GetOriginalName()));

            FieldDefinition f_this = null;
            FieldDefinition f_completeArea = null;

            MethodDefinition m_IsCompleteArea = method.DeclaringType.FindMethod("System.Boolean IsCompleteArea(System.Boolean)");
            if (m_IsCompleteArea == null)
                return;

            // The gem collection routine is stored in a compiler-generated method.
            foreach (TypeDefinition nest in method.DeclaringType.NestedTypes) {
                if (!nest.Name.StartsWith("<CollectRoutine>d__"))
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

            MethodDefinition m_CanChangeMusic = method.DeclaringType.FindMethod("System.Boolean _CanChangeMusic(System.Boolean,Celeste.BadelineOldsite)");
            if (m_CanChangeMusic == null)
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

                // No need to check for the full name when the field name itself is compiler-generated.
                if (instri > 3 &&
                    instrs[instri - 2].OpCode == OpCodes.Ldfld && (instrs[instri - 2].Operand as FieldReference)?.FullName == "System.String Celeste.Session::Level" &&
                    instrs[instri - 1].OpCode == OpCodes.Ldstr && (instrs[instri - 1].Operand as string) == "2" &&
                    instr.OpCode == OpCodes.Call && (instr.Operand as MethodReference)?.GetID() == "System.Boolean System.String::op_Equality(System.String,System.String)"
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

                if (instri > 3 &&
                    instrs[instri - 2].OpCode == OpCodes.Ldfld && (instrs[instri - 2].Operand as FieldReference)?.FullName == "Celeste.AreaMode Celeste.AreaKey::Mode" &&
                    instrs[instri - 1].OpCode == OpCodes.Ldc_I4_0 &&
                    instr.OpCode == OpCodes.Ceq
                ) {
                    // After ==, process the result.
                    instri++;
                    // Push this and grab this from this.
                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;
                    instrs.Insert(instri, il.Create(OpCodes.Ldfld, f_this));
                    instri++;
                    // Process.
                    instrs.Insert(instri, il.Create(OpCodes.Call, m_CanChangeMusic));
                    instri++;
                }

                // Alternatively:

                if (instri > 2 &&
                    instrs[instri - 1].OpCode == OpCodes.Ldfld && (instrs[instri - 1].Operand as FieldReference)?.FullName == "Celeste.AreaMode Celeste.AreaKey::Mode" &&
                    (instr.OpCode == OpCodes.Brtrue || instr.OpCode == OpCodes.Brtrue_S)
                ) {
                    // Before brtrue
                    // Insert == 0
                    instrs.Insert(instri, il.Create(OpCodes.Ldc_I4_0));
                    instri++;
                    instrs.Insert(instri, il.Create(OpCodes.Ceq));
                    // After ==, process the result.
                    instri++;
                    // Push this and grab this from this.
                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;
                    instrs.Insert(instri, il.Create(OpCodes.Ldfld, f_this));
                    instri++;
                    // Process.
                    instrs.Insert(instri, il.Create(OpCodes.Call, m_CanChangeMusic));
                    instri++;
                    // Move back to brtrue
                    instri++;
                    // Replace brtrue with brfalse
                    instr.OpCode = OpCodes.Brfalse;
                }

            }

        }

        public static void PatchBadelineBossOnPlayer(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            MethodDefinition m_CanChangeMusic = method.DeclaringType.FindMethod("System.Boolean _CanChangeMusic(System.Boolean,Celeste.FinalBoss)");
            if (m_CanChangeMusic == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instri > 3 &&
                    instrs[instri - 2].OpCode == OpCodes.Ldfld && (instrs[instri - 2].Operand as FieldReference)?.FullName == "Celeste.AreaMode Celeste.AreaKey::Mode" &&
                    instrs[instri - 1].OpCode == OpCodes.Ldc_I4_0 &&
                    instr.OpCode == OpCodes.Ceq
                ) {
                    // After ==, process the result.
                    instri++;
                    // Grab this.
                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;
                    // Process.
                    instrs.Insert(instri, il.Create(OpCodes.Call, m_CanChangeMusic));
                    instri++;
                }

                // Alternatively:

                if (instri > 2 &&
                    instrs[instri - 1].OpCode == OpCodes.Ldfld && (instrs[instri - 1].Operand as FieldReference)?.FullName == "Celeste.AreaMode Celeste.AreaKey::Mode" &&
                    (instr.OpCode == OpCodes.Brtrue || instr.OpCode == OpCodes.Brtrue_S)
                ) {
                    // Before brtrue
                    // Insert == 0
                    instrs.Insert(instri, il.Create(OpCodes.Ldc_I4_0));
                    instri++;
                    instrs.Insert(instri, il.Create(OpCodes.Ceq));
                    // After ==, process the result.
                    instri++;
                    // Grab this.
                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;
                    // Process.
                    instrs.Insert(instri, il.Create(OpCodes.Call, m_CanChangeMusic));
                    instri++;
                    // Move back to brtrue
                    instri++;
                    // Replace brtrue with brfalse
                    instr.OpCode = OpCodes.Brfalse;
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
                    (instr.OpCode == OpCodes.Brfalse || instr.OpCode == OpCodes.Brfalse_S)
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

        public static void PatchRainFGRender(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            MethodDefinition m_GetColor = method.DeclaringType.FindMethod("Microsoft.Xna.Framework.Color _GetColor(System.String,Celeste.RainFG)");
            if (m_GetColor == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Call && (instr.Operand as MethodReference)?.GetID() == "Microsoft.Xna.Framework.Color Monocle.Calc::HexToColor(System.String)") {
                    // Push this.
                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;
                    // Replace the method call.
                    instr.Operand = m_GetColor;
                    instri++;
                }
            }
        }

        public static void PatchDialogLoader(MethodDefinition method, CustomAttribute attrib) {
            // Our actual target method is the orig_ method.
            method = method.DeclaringType.FindMethod(method.GetID(name: method.GetOriginalName()));

            MethodDefinition m_GetFiles = method.DeclaringType.FindMethod("System.String[] _GetFiles(System.String,System.String,System.IO.SearchOption)");
            if (m_GetFiles == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Call && (instr.Operand as MethodReference)?.GetID() == "System.String[] System.IO.Directory::GetFiles(System.String,System.String,System.IO.SearchOption)") {
                    instr.Operand = m_GetFiles;
                }
            }
        }

        public static void PatchLoadLanguage(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_GetLanguageText = method.DeclaringType.FindMethod("System.Collections.Generic.IEnumerable`1<System.String> _GetLanguageText(System.String,System.Text.Encoding)");
            if (m_GetLanguageText == null)
                return;

            MethodDefinition m_NewLanguage = method.DeclaringType.FindMethod("Celeste.Language _NewLanguage()");
            if (m_NewLanguage == null)
                return;

            MethodDefinition m_SetItem = method.DeclaringType.FindMethod("System.Void _SetItem(System.Collections.Generic.Dictionary`2<System.String,System.String>,System.String,System.String,Celeste.Language)");
            if (m_SetItem == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Call && (instr.Operand as MethodReference)?.GetID() == "System.Collections.Generic.IEnumerable`1<System.String> System.IO.File::ReadLines(System.String,System.Text.Encoding)") {
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_GetLanguageText;
                }

                if (instr.OpCode == OpCodes.Newobj && (instr.Operand as MethodReference)?.GetID() == "System.Void Celeste.Language::.ctor()") {
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_NewLanguage;
                }

                if (instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference)?.GetID() == "System.Void System.Collections.Generic.Dictionary`2<System.String,System.String>::set_Item(System.Collections.Generic.Dictionary`2<System.String,System.String>/!0,System.Collections.Generic.Dictionary`2<System.String,System.String>/!1)") {
                    // Push the language object. Should always be stored in the first local var.
                    instrs.Insert(instri, il.Create(OpCodes.Ldloc_0));
                    instri++;
                    // Replace the method call.
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_SetItem;
                }
            }

        }

        public static void PatchInitMMSharedData(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_Set = method.DeclaringType.FindMethod("System.Void SetMMSharedData(System.String,System.Boolean)");
            if (m_Set == null)
                return;

            if (!method.HasBody)
                return;

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
                string callingID = calling?.GetID();

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
                string callingID = calling?.GetID();

                // The matching CompleteRenderer .ctor has been added manually, thus manually relink to it.
                if (instr.OpCode != OpCodes.Newobj || (
                    callingID != "System.Void Celeste.CompleteRenderer::.ctor(System.Xml.XmlElement,Monocle.Atlas,System.Single,System.Action)"
                ))
                    continue;

                instr.Operand = calling.DeclaringType.Resolve().FindMethod("System.Void Celeste.CompleteRenderer::.ctor(System.Xml.XmlElement,Monocle.Atlas,System.Single,System.Action,Celeste.Mod.Meta.MapMetaCompleteScreen)");

                instrs.Insert(instri, il.Create(OpCodes.Ldarg, paramMeta));
                instri++;
            }

            ILCursor cursor = new ILCursor(new ILContext(method));
            MethodDefinition m_GetCustomCompleteScreenTitle = method.DeclaringType.FindMethod("System.String GetCustomCompleteScreenTitle()");

            int textVariableIndex = 0;

            /*
             * // string text = Dialog.Clean("areacomplete_" + session.Area.Mode + (session.FullClear ? "_fullclear" : ""), null);
             * IL_005D: ldstr     "areacomplete_"
             * IL_0062: ldarg.1
             * IL_0063: ldflda    valuetype Celeste.AreaKey Celeste.Session::Area
             * ...
             * IL_008B: ldnull
             * IL_008C: call      string Celeste.Dialog::Clean(string, class Celeste.Language)
             * IL_0091: stloc.1
             *
             * // Vector2 origin = new Vector2(960f, 200f);
             * IL_0092: ldloca.s  V_2
             * IL_0094: ldc.r4    960
             * ...
             */

            // move the cursor to IL_0092 and find the variable index of "text"
            if (cursor.TryGotoNext(MoveType.After,
                instr => (instr.Operand as MethodReference)?.FullName == "System.String Celeste.Dialog::Clean(System.String,Celeste.Language)",
                instr => instr.MatchStloc(out textVariableIndex) && il.Body.Variables[textVariableIndex].VariableType.FullName == "System.String")) {

                // mark for later use
                ILLabel target = cursor.MarkLabel();
                // go back to IL_005D
                if (cursor.TryGotoPrev(MoveType.Before,
                    instr => instr.MatchLdstr("areacomplete_"))) {

                    // equivalent to "text = this.GetCustomCompleteScreenTitle()"
                    cursor.Emit(OpCodes.Ldarg_0);
                    cursor.Emit(OpCodes.Call, m_GetCustomCompleteScreenTitle);
                    cursor.Emit(OpCodes.Stloc_S, (byte) textVariableIndex);

                    // wrap the original text assignment code in "if (text == null)", fallback to original if no custom title in meta.yaml
                    cursor.Emit(OpCodes.Ldloc_S, (byte) textVariableIndex);
                    cursor.Emit(OpCodes.Brtrue_S, target.Target);
                }
            }
        }


        public static void PatchGameLoaderIntroRoutine(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_GetNextScene = method.DeclaringType.FindMethod("Monocle.Scene _GetNextScene(Celeste.Overworld/StartMode,Celeste.HiresSnow)");
            if (m_GetNextScene == null)
                return;

            // The routine is stored in a compiler-generated method.
            foreach (TypeDefinition nest in method.DeclaringType.NestedTypes) {
                if (!nest.Name.StartsWith("<" + method.Name + ">d__"))
                    continue;
                method = nest.FindMethod("System.Boolean MoveNext()") ?? method;
                break;
            }

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Newobj && (instr.Operand as MethodReference)?.GetID() == "System.Void Celeste.OverworldLoader::.ctor(Celeste.Overworld/StartMode,Celeste.HiresSnow)") {
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_GetNextScene;
                }
            }
        }

        public static void PatchSaveRoutine(MethodDefinition method, CustomAttribute attrib) {
            if (SaveData == null)
                SaveData = MonoModRule.Modder.FindType("Celeste.SaveData")?.Resolve();
            if (SaveData == null)
                return;

            FieldDefinition f_Instance = SaveData.FindField("Instance");
            if (f_Instance == null)
                return;

            MethodDefinition m_AfterInitialize = SaveData.FindMethod("System.Void AfterInitialize()");
            if (m_AfterInitialize == null)
                return;

            // The routine is stored in a compiler-generated method.
            foreach (TypeDefinition nest in method.DeclaringType.NestedTypes) {
                if (!nest.Name.StartsWith("<" + method.Name + ">d__"))
                    continue;
                method = nest.FindMethod("System.Boolean MoveNext()") ?? method;
                break;
            }

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Call && (instr.Operand as MethodReference)?.GetID() == "System.Byte[] Celeste.UserIO::Serialize<Celeste.SaveData>(T)") {
                    instri++;

                    instrs.Insert(instri, il.Create(OpCodes.Ldsfld, f_Instance));
                    instri++;

                    instrs.Insert(instri, il.Create(OpCodes.Callvirt, m_AfterInitialize));
                    instri++;
                }
            }
        }

        public static void PatchPlayerOrigUpdate(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_IsOverWater = method.DeclaringType.FindMethod("System.Boolean _IsOverWater()");
            if (m_IsOverWater == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 1; instri < instrs.Count - 5; instri++) {
                // turn "if (Speed.Y < 0f && Speed.Y >= -60f)" into "if (Speed.Y < 0f && Speed.Y >= -60f && _IsOverWater())"
                if (instrs[instri].OpCode == OpCodes.Ldarg_0
                    && instrs[instri + 1].OpCode == OpCodes.Ldflda && (instrs[instri + 1].Operand as FieldReference)?.FullName == "Microsoft.Xna.Framework.Vector2 Celeste.Player::Speed"
                    && instrs[instri + 2].OpCode == OpCodes.Ldfld && (instrs[instri + 2].Operand as FieldReference)?.FullName == "System.Single Microsoft.Xna.Framework.Vector2::Y"
                    && instrs[instri + 3].OpCode == OpCodes.Ldc_R4 && (float) instrs[instri + 3].Operand == -60f) {

                    // XNA:
                    // 0: ldarg.0
                    // 1: ldflda Celeste.Player::Speed
                    // 2: ldfld Vector2::Y
                    // 3: ldc.r4 -60
                    // 4: blt.un [instruction after if]
                    if (instrs[instri + 4].OpCode == OpCodes.Blt_Un) {
                        // 5: ldarg.0
                        // 6: call Player::_IsOverWater
                        // 7: brfalse [instruction after if]
                        instrs.Insert(instri + 5, il.Create(OpCodes.Ldarg_0));
                        instrs.Insert(instri + 6, il.Create(OpCodes.Call, m_IsOverWater));
                        instrs.Insert(instri + 7, il.Create(OpCodes.Brfalse, instrs[instri + 4].Operand));
                    }

                    // FNA:
                    // -1: bge.un.s [instruction setting flag to false]
                    // 0: ldarg.0
                    // 1: ldflda Celeste.Player::Speed
                    // 2: ldfld Vector2::Y
                    // 3: ldc.r4 -60
                    // 4: clt.un
                    // 5: ldc.i4.0
                    // 6: ceq [final value for the flag. if this flag is true, we enter the if]
                    if (instrs[instri - 1].OpCode == OpCodes.Bge_Un_S
                        && instrs[instri + 4].OpCode == OpCodes.Clt_Un
                        && instrs[instri + 5].OpCode == OpCodes.Ldc_I4_0) {
                        // 4: blt.un [instruction setting flag to false]
                        // 5: ldarg.0
                        // 6: call Player::_IsOverWater
                        // 7: ldc.i4.1
                        // 8: ceq
                        instrs[instri + 4].OpCode = OpCodes.Blt_Un;
                        instrs[instri + 4].Operand = instrs[instri - 1].Operand;

                        instrs.Insert(instri + 5, il.Create(OpCodes.Ldarg_0));
                        instrs.Insert(instri + 6, il.Create(OpCodes.Call, m_IsOverWater));

                        instrs[instri + 7].OpCode = OpCodes.Ldc_I4_1;

                    }
                }
            }
        }

        public static void PatchChapterPanelSwapRoutine(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_GetCheckpoints = method.DeclaringType.FindMethod("System.Collections.Generic.HashSet`1<System.String> _GetCheckpoints(Celeste.SaveData,Celeste.AreaKey)");
            if (m_GetCheckpoints == null)
                return;

            // The gem collection routine is stored in a compiler-generated method.
            foreach (TypeDefinition nest in method.DeclaringType.NestedTypes) {
                if (!nest.Name.StartsWith("<SwapRoutine>d__"))
                    continue;
                method = nest.FindMethod("System.Boolean MoveNext()") ?? method;
                break;
            }

            if (!method.HasBody)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 1; instri < instrs.Count - 5; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference)?.GetID() == "System.Collections.Generic.HashSet`1<System.String> Celeste.SaveData::GetCheckpoints(Celeste.AreaKey)") {
                    // Replace the method call.
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_GetCheckpoints;
                    instri++;
                }
            }
        }

        public static void PatchStrawberryInterface(ICustomAttributeProvider provider, CustomAttribute attrib) {
            // MonoModRule.Modder.FindType("Celeste.Mod.IStrawberry");
            if (IStrawberry == null) {
                IStrawberry = new InterfaceImplementation(MonoModRule.Modder.FindType("Celeste.Mod.IStrawberry"));
            }
            if (IStrawberry == null)
                return;

            ((TypeDefinition) provider).Interfaces.Add(IStrawberry);
        }

        public static void PatchInterface(MethodDefinition method, CustomAttribute attrib) {
            MethodAttributes flags = MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot;
            method.Attributes |= flags;
        }

        public static void PatchFileSelectSlotRender(MethodDefinition method, CustomAttribute attrib) {
            FieldDefinition f_maxStrawberryCount = method.DeclaringType.FindField("maxStrawberryCount");
            if (f_maxStrawberryCount == null)
                return;

            FieldDefinition f_maxGoldenStrawberryCount = method.DeclaringType.FindField("maxGoldenStrawberryCount");
            if (f_maxGoldenStrawberryCount == null)
                return;

            FieldDefinition f_maxCassettes = method.DeclaringType.FindField("maxCassettes");
            if (f_maxCassettes == null)
                return;

            FieldDefinition f_maxCrystalHeartsExcludingCSides = method.DeclaringType.FindField("maxCrystalHeartsExcludingCSides");
            if (f_maxCrystalHeartsExcludingCSides == null)
                return;

            FieldDefinition f_maxCrystalHearts = method.DeclaringType.FindField("maxCrystalHearts");
            if (f_maxCrystalHearts == null)
                return;

            FieldDefinition f_summitStamp = method.DeclaringType.FindField("summitStamp");
            if (f_summitStamp == null)
                return;

            FieldDefinition f_farewellStamp = method.DeclaringType.FindField("farewellStamp");
            if (f_farewellStamp == null)
                return;

            FieldDefinition f_totalGoldenStrawberries = method.DeclaringType.FindField("totalGoldenStrawberries");
            if (f_totalGoldenStrawberries == null)
                return;

            FieldDefinition f_totalHeartGems = method.DeclaringType.FindField("totalHeartGems");
            if (f_totalHeartGems == null)
                return;

            FieldDefinition f_totalCassettes = method.DeclaringType.FindField("totalCassettes");
            if (f_totalCassettes == null)
                return;


            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count - 8; instri++) {
                if (instrs[instri].OpCode == OpCodes.Ldc_I4 && (int) instrs[instri].Operand == 175) {
                    instrs[instri].OpCode = OpCodes.Ldarg_0;
                    instrs.Insert(instri + 1, il.Create(OpCodes.Ldfld, f_maxStrawberryCount));
                }

                if (instrs[instri].OpCode == OpCodes.Ldc_I4_8) {
                    instrs[instri].OpCode = OpCodes.Ldarg_0;
                    instrs.Insert(instri + 1, il.Create(OpCodes.Ldfld, f_maxCassettes));
                }

                if (instrs[instri].OpCode == OpCodes.Ldfld && (instrs[instri].Operand as FieldReference).Name == "SaveData"
                    && instrs[instri + 1].OpCode == OpCodes.Callvirt && (instrs[instri + 1].Operand as MethodReference).Name == "get_TotalHeartGems"
                    && instrs[instri + 2].OpCode == OpCodes.Ldc_I4_S && (sbyte) instrs[instri + 2].Operand == 16) {

                    instrs[instri].OpCode = OpCodes.Ldfld;
                    instrs[instri].Operand = f_totalHeartGems;

                    instrs[instri + 1].OpCode = OpCodes.Ldarg_0;

                    instrs[instri + 2].OpCode = OpCodes.Ldfld;
                    instrs[instri + 2].Operand = f_maxCrystalHeartsExcludingCSides;
                }

                if (instrs[instri].OpCode == OpCodes.Ldc_I4_S && (sbyte) instrs[instri].Operand == 24) {
                    instrs[instri].OpCode = OpCodes.Ldarg_0;
                    instrs.Insert(instri + 1, il.Create(OpCodes.Ldfld, f_maxCrystalHearts));
                }

                if (instrs[instri].OpCode == OpCodes.Ldc_I4_S && (sbyte) instrs[instri].Operand == 25) {
                    instrs[instri].OpCode = OpCodes.Ldarg_0;
                    instrs.Insert(instri + 1, il.Create(OpCodes.Ldfld, f_maxGoldenStrawberryCount));
                }

                // here is what we want to replace: this.SaveData.Areas_Safe[7 or 10].Modes[0].Completed;
                if (instrs[instri].OpCode == OpCodes.Ldarg_0
                    && instrs[instri + 1].OpCode == OpCodes.Ldfld && (instrs[instri + 1].Operand as FieldReference).Name == "SaveData"
                    && instrs[instri + 2].OpCode == OpCodes.Callvirt && (instrs[instri + 2].Operand as MethodReference).Name == "get_Areas_Safe"
                    // instrs[instri + 3] = ldc.i4 7 or 10
                    && instrs[instri + 4].OpCode == OpCodes.Callvirt && (instrs[instri + 4].Operand as MethodReference).Name == "get_Item"
                    && instrs[instri + 5].OpCode == OpCodes.Ldfld && (instrs[instri + 5].Operand as FieldReference).Name == "Modes"
                    && instrs[instri + 6].OpCode == OpCodes.Ldc_I4_0
                    && instrs[instri + 7].OpCode == OpCodes.Ldelem_Ref
                    && instrs[instri + 8].OpCode == OpCodes.Ldfld && (instrs[instri + 8].Operand as FieldReference).Name == "Completed") {

                    if (instrs[instri + 3].OpCode == OpCodes.Ldc_I4_7) {
                        // remove everything but this
                        instri++;
                        for (int i = 0; i < 8; i++)
                            instrs.RemoveAt(instri);

                        // and put summitStamp instead
                        instrs.Insert(instri, il.Create(OpCodes.Ldfld, f_summitStamp));

                    }

                    if (instrs[instri + 3].OpCode == OpCodes.Ldc_I4_S && (sbyte) instrs[instri + 3].Operand == 10) {
                        // remove everything but this
                        instri++;
                        for (int i = 0; i < 8; i++)
                            instrs.RemoveAt(instri);

                        // and put farewellStamp instead
                        instrs.Insert(instri, il.Create(OpCodes.Ldfld, f_farewellStamp));
                    }
                }

                if (instrs[instri].OpCode == OpCodes.Ldfld && (instrs[instri].Operand as FieldReference).Name == "SaveData"
                    && instrs[instri + 1].OpCode == OpCodes.Ldfld && (instrs[instri + 1].Operand as FieldReference).Name == "TotalGoldenStrawberries") {

                    instrs.RemoveAt(instri);
                    instrs[instri].Operand = f_totalGoldenStrawberries;
                }

                if (instrs[instri].OpCode == OpCodes.Ldfld && (instrs[instri].Operand as FieldReference).Name == "SaveData"
                    && instrs[instri + 1].OpCode == OpCodes.Callvirt && (instrs[instri + 1].Operand as MethodReference).Name == "get_TotalHeartGems") {

                    instrs.RemoveAt(instri);
                    instrs[instri].OpCode = OpCodes.Ldfld;
                    instrs[instri].Operand = f_totalHeartGems;
                }

                if (instrs[instri].OpCode == OpCodes.Ldfld && (instrs[instri].Operand as FieldReference).Name == "SaveData"
                    && instrs[instri + 1].OpCode == OpCodes.Callvirt && (instrs[instri + 1].Operand as MethodReference).Name == "get_TotalCassettes") {

                    instrs.RemoveAt(instri);
                    instrs[instri].OpCode = OpCodes.Ldfld;
                    instrs[instri].Operand = f_totalCassettes;
                }
            }
        }

        public static void PatchPathfinderRender(MethodDefinition method, CustomAttribute attrib) {
            FieldDefinition f_map = method.DeclaringType.FindField("map");
            if (f_map == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            bool firstDimension = true;
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Ldc_I4 && ((int) instr.Operand) == 200) {
                    // replace 200 with a call to get the array length.
                    instr.OpCode = OpCodes.Ldarg_0;
                    instrs.Insert(instri + 1, il.Create(OpCodes.Ldfld, f_map));
                    instrs.Insert(instri + 2, il.Create(firstDimension ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
                    instrs.Insert(instri + 3, il.Create(OpCodes.Callvirt, typeof(Array).GetMethod("GetLength")));

                    instri += 3;
                    firstDimension = false;
                }
            }
        }

        public static void PatchTotalHeartGemChecks(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_getTotalHeartGemsInVanilla = method.Module.GetType("Celeste.SaveData")?.FindMethod("System.Int32 get_TotalHeartGemsInVanilla()");
            if (m_getTotalHeartGemsInVanilla == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Callvirt && ((MethodReference) instr.Operand).Name == "get_TotalHeartGems") {
                    // replace the call to the TotalHeartGems property with a call to TotalHeartGemsInVanilla.
                    instr.Operand = m_getTotalHeartGemsInVanilla;
                }
            }
        }
        public static void PatchTotalHeartGemChecksInRoutine(MethodDefinition method, CustomAttribute attrib) {
            // Routines are stored in compiler-generated methods.
            foreach (TypeDefinition nest in method.DeclaringType.NestedTypes) {
                if (!nest.Name.StartsWith("<" + method.Name + ">d__"))
                    continue;
                method = nest.FindMethod("System.Boolean MoveNext()") ?? method;
                break;
            }

            PatchTotalHeartGemChecks(method, attrib);
        }

        public static void PatchOuiJournalStatsHeartGemCheck(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_getUnlockedModes = method.Module.GetType("Celeste.SaveData")?.FindMethod("System.Int32 get_UnlockedModes()");
            if (m_getUnlockedModes == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count - 1; instri++) {
                Instruction instr = instrs[instri];
                Instruction nextInstr = instrs[instri + 1];

                if (instr.OpCode == OpCodes.Callvirt && ((MethodReference) instr.Operand).Name == "get_TotalHeartGems"
                    && nextInstr.OpCode == OpCodes.Ldc_I4_S && ((sbyte) nextInstr.Operand) == 16) {

                    // instead of SaveData.Instance.TotalHeartGems >= 16, we want SaveData.Instance.UnlockedModes >= 3.
                    // this way, we only display the golden berry stat when golden berries are actually unlocked in the level set we are in.
                    // (UnlockedModes returns 3 if and only if TotalHeartGems is more than 16 in the vanilla level set anyway.)
                    instr.Operand = m_getUnlockedModes;
                    nextInstr.OpCode = OpCodes.Ldc_I4_3;
                }
            }
        }

        public static void MakeMethodPublic(MethodDefinition method, CustomAttribute attrib) {
            method.SetPublic(true);
        }

        public static void PatchSpinnerCreateSprites(MethodDefinition method, CustomAttribute attrib) {
            FieldDefinition f_ID = method.DeclaringType.FindField("ID");
            if (f_ID == null)
                return;

            ILProcessor il = method.Body.GetILProcessor();
            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count - 5; instri++) {
                if (instrs[instri].OpCode == OpCodes.Ldloc_S
                    && instrs[instri + 1].OpCode == OpCodes.Ldarg_0
                    && instrs[instri + 2].OpCode == OpCodes.Beq_S) {

                    // instead of comparing the X positions for spinners, compare their IDs.
                    // this way, we are sure spinner 1 will connect to spinner 2, but spinner 2 won't connect to spinner 1.
                    instrs.Insert(instri + 1, il.Create(OpCodes.Ldfld, f_ID));
                    instrs.Insert(instri + 3, il.Create(OpCodes.Ldfld, f_ID));
                    instrs[instri + 4].OpCode = OpCodes.Ble_S;
                }

                if (instrs[instri].OpCode == OpCodes.Ldloc_S
                    && instrs[instri + 1].OpCode == OpCodes.Callvirt && (instrs[instri + 1].Operand as MethodReference).Name == "get_X"
                    && instrs[instri + 2].OpCode == OpCodes.Ldarg_0
                    && instrs[instri + 3].OpCode == OpCodes.Call && (instrs[instri + 3].Operand as MethodReference).Name == "get_X"
                    && instrs[instri + 4].OpCode == OpCodes.Blt_Un_S) {

                    // the other.X >= this.X check is made redundant by the patch above. Remove it.
                    for (int i = 0; i < 5; i++) {
                        instrs.RemoveAt(instri);
                    }
                }

                // replace "(item.Position - Position).Length() < 24f" with "(item.Position - Position).LengthSquared() < 576f".
                // this is equivalent, except it skips a square root calculation, which helps with performance.
                if (instrs[instri].OpCode == OpCodes.Call && ((MethodReference) instrs[instri].Operand).Name == "Length") {
                    ((MethodReference) instrs[instri].Operand).Name = "LengthSquared";
                }
                if (instrs[instri].OpCode == OpCodes.Ldc_R4 && ((float) instrs[instri].Operand) == 24f) {
                    instrs[instri].Operand = 576f;
                }
            }
        }

        public static void PatchOuiFileSelectSubmenuChecks(MethodDefinition method, CustomAttribute attrib) {
            TypeDefinition t_ISubmenu = method.Module.GetType("Celeste.OuiFileSelectSlot/ISubmenu");
            if (t_ISubmenu == null)
                return;

            // The routine is stored in a compiler-generated method.
            string methodName = method.Name;
            if (methodName.StartsWith("orig_")) {
                methodName = methodName.Substring(5);
            }
            foreach (TypeDefinition nest in method.DeclaringType.NestedTypes) {
                if (!nest.Name.StartsWith("<" + methodName + ">d__"))
                    continue;
                method = nest.FindMethod("System.Boolean MoveNext()") ?? method;
                break;
            }

            ILProcessor il = method.Body.GetILProcessor();
            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count - 4; instri++) {
                if (instrs[instri].OpCode == OpCodes.Brtrue_S
                    && instrs[instri + 1].OpCode == OpCodes.Ldarg_0
                    && instrs[instri + 2].OpCode == OpCodes.Ldfld
                    && instrs[instri + 3].OpCode == OpCodes.Isinst && ((TypeDefinition) instrs[instri + 3].Operand).Name == "OuiAssistMode") {

                    // gather some info
                    FieldReference field = (FieldReference) instrs[instri + 2].Operand;
                    Instruction branchTarget = (Instruction) instrs[instri].Operand;

                    // then inject another similar check for ISubmenu
                    instri++;
                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, field));
                    instrs.Insert(instri++, il.Create(OpCodes.Isinst, t_ISubmenu));
                    instrs.Insert(instri++, il.Create(OpCodes.Brtrue_S, branchTarget));

                } else if (instrs[instri].OpCode == OpCodes.Ldarg_0
                    && instrs[instri + 1].OpCode == OpCodes.Ldfld
                    && instrs[instri + 2].OpCode == OpCodes.Isinst && ((TypeDefinition) instrs[instri + 2].Operand).Name == "OuiAssistMode"
                    && instrs[instri + 3].OpCode == OpCodes.Brfalse_S) {

                    // gather some info
                    FieldReference field = (FieldReference) instrs[instri + 1].Operand;
                    Instruction branchTarget = instrs[instri + 4];

                    // then inject another similar check for ISubmenu
                    instri++;
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, field));
                    instrs.Insert(instri++, il.Create(OpCodes.Isinst, t_ISubmenu));
                    instrs.Insert(instri++, il.Create(OpCodes.Brtrue_S, branchTarget));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                }
            }
        }

        public static void PatchFontsPrepare(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_GetFiles = method.DeclaringType.FindMethod("System.String[] _GetFiles(System.String,System.String,System.IO.SearchOption)");
            if (m_GetFiles == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count; instri++) {
                if (instrs[instri].OpCode == OpCodes.Call && (instrs[instri].Operand as MethodReference).Name == "GetFiles") {
                    instrs[instri].Operand = m_GetFiles;
                }
            }
        }

        public static void MakeEntryPoint(MethodDefinition method, CustomAttribute attrib) {
            MonoModRule.Modder.Module.EntryPoint = method;
        }

        public static void PatchCelesteMain(MethodDefinition method, CustomAttribute attrib) {
            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count; instri++) {
                if (instrs[instri].OpCode == OpCodes.Call && (instrs[instri].Operand as MethodReference)?.GetID() == "System.String SDL2.SDL::SDL_GetPlatform()") {
                    instrs[instri].OpCode = OpCodes.Ldstr;
                    instrs[instri].Operand = "Windows";
                }
            }
        }

        public static void RemoveCommandAttributeFromVanillaLoadMethod(MethodDefinition method, CustomAttribute attrib) {
            // find the vanilla method: CmdLoadIDorSID(string, string) => CmdLoad(int, string)
            string vanillaMethodName = method.Name.Replace("IDorSID", "");
            Mono.Collections.Generic.Collection<CustomAttribute> attributes = method.DeclaringType.FindMethod($"System.Void {vanillaMethodName}(System.Int32,System.String)").CustomAttributes;
            for (int i = 0; i < attributes.Count; i++) {
                // remove all Command attributes.
                if (attributes[i]?.AttributeType.FullName == "Monocle.Command") {
                    attributes.RemoveAt(i);
                    i--;
                }
            }
        }

        public static void PatchFakeHeartColor(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_getCustomColor = method.DeclaringType.FindMethod("Celeste.AreaMode _getCustomColor(Celeste.AreaMode,Celeste.FakeHeart)");
            if (m_getCustomColor == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Call && ((MethodReference) instr.Operand).Name == "Choose") {
                    instrs.Insert(instri + 1, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri + 2, il.Create(OpCodes.Call, m_getCustomColor));
                }
            }
        }

        public static void PatchOuiFileNamingRendering(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_shouldDisplaySwitchAlphabetPrompt = method.DeclaringType.FindMethod("System.Boolean _shouldDisplaySwitchAlphabetPrompt()");
            if (m_shouldDisplaySwitchAlphabetPrompt == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Callvirt && ((MethodReference) instr.Operand).Name == "get_Japanese") {
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_shouldDisplaySwitchAlphabetPrompt;
                }
            }
        }

        public static void PatchRumbleTriggerAwake(MethodDefinition method, CustomAttribute attrig) {
            MethodDefinition m_entity_get_Y = MonoModRule.Modder.FindType("Monocle.Entity").Resolve().FindMethod("get_Y");
            if (m_entity_get_Y == null)
                return;

            FieldDefinition f_constrainHeight = method.DeclaringType.FindField("constrainHeight");
            FieldDefinition f_top = method.DeclaringType.FindField("top");
            FieldDefinition f_bottom = method.DeclaringType.FindField("bottom");

            if (f_constrainHeight == null || f_top == null || f_bottom == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];
                /*
                        ldloc.*
                        callvirt	instance float32 Monocle.Entity::get_X()
                        ldarg.0
                        ldfld	float32 Celeste.RumbleTrigger::left
                        blt.un.s	ldloca.s
                        ldloc.*
                        callvirt	instance float32 Monocle.Entity::get_X()
                        ldarg.0
                        ldfld	float32 Celeste.RumbleTrigger::right // We are here
                        bgt.un.s	ldloca.s
                     */
                if (instr.OpCode == OpCodes.Ldfld && ((FieldReference) instr.Operand).Name == "right") {
                    Instruction noYConstraintTarget = instrs[instri - 8];

                    // Copy relevant instructions and modify as needed
                    Instruction[] instrCopy = new Instruction[10];
                    for (int i = 0; i < 10; i++) {
                        instrCopy[i] = il.Create(instrs[instri + i - 8].OpCode, instrs[instri + i - 8].Operand);
                        if (instrCopy[i].OpCode == OpCodes.Callvirt)
                            instrCopy[i].Operand = m_entity_get_Y;
                        if (instrCopy[i].OpCode == OpCodes.Ldfld && ((FieldReference) instrCopy[i].Operand).Name == "left")
                            instrCopy[i].Operand = f_top;
                        if (instrCopy[i].OpCode == OpCodes.Ldfld && ((FieldReference) instrCopy[i].Operand).Name == "right")
                            instrCopy[i].Operand = f_bottom;
                        if (instrCopy[i].OpCode == OpCodes.Cgt_Un) {
                            // we are in Steam FNA, and want to replace this with a blg.un.s with the same target as 5 instructions before
                            instrCopy[i].OpCode = OpCodes.Bgt_Un_S;
                            instrCopy[i].Operand = instrCopy[i - 5].Operand;
                        }
                    }

                    instri -= 8;
                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_constrainHeight));
                    instrs.Insert(instri++, il.Create(OpCodes.Brfalse_S, noYConstraintTarget));

                    // Insert copied instructions
                    instrs.InsertRange(instri, instrCopy);
                    instri += instrCopy.Length;

                    instri += 8;
                }
            }

            // Fix some issues with short-form branch instructions now being out of range
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];
                if (instr.Operand is Instruction i &&
                    Math.Abs(i.Offset - instr.Offset) > 102) {
                    instr.OpCode = instr.OpCode.ToLongOp();
                }
            }
        }

        public static void PatchEventTriggerOnEnter(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            MethodDefinition m_TriggerCustomEvent = method.DeclaringType.FindMethod("System.Boolean TriggerCustomEvent(Celeste.EventTrigger,Celeste.Player,System.String)");
            if (m_TriggerCustomEvent == null)
                return;

            FieldDefinition f_Event = method.DeclaringType.FindField("Event");
            if (f_Event == null)
                return;

            // We also need to do special work in the cctor.
            MethodDefinition m_cctor = method.DeclaringType.FindMethod(".cctor");
            if (m_cctor == null)
                return;

            FieldDefinition f_LoadStrings = method.DeclaringType.FindField("_LoadStrings");
            if (f_LoadStrings == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> cctor_instrs = m_cctor.Body.Instructions;
            ILProcessor cctor_il = m_cctor.Body.GetILProcessor();

            // Remove cctor ret for simplicity. Re-add later.
            cctor_instrs.RemoveAt(cctor_instrs.Count - 1);

            TypeDefinition td_LoadStrings = f_LoadStrings.FieldType.Resolve();
            MethodReference m_LoadStrings_Add = MonoModRule.Modder.Module.ImportReference(td_LoadStrings.FindMethod("Add"));
            m_LoadStrings_Add.DeclaringType = f_LoadStrings.FieldType;
            MethodReference m_LoadStrings_ctor = MonoModRule.Modder.Module.ImportReference(td_LoadStrings.FindMethod("System.Void .ctor()"));
            m_LoadStrings_ctor.DeclaringType = f_LoadStrings.FieldType;
            cctor_il.Emit(OpCodes.Newobj, m_LoadStrings_ctor);

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                /* We expect something similar enough to the following:
                ldfld     string Celeste.EventTrigger::Event // We're here
                stloc*
                ldloc*
                call      uint32 '<PrivateImplementationDetails>'::ComputeStringHash(string)

                Note that MonoMod requires the full type names (System.UInt32 instead of uint32) and skips escaping 's
                */

                if (instri > 0 &&
                        instri < instrs.Count - 4 &&
                        instr.OpCode == OpCodes.Ldfld && (instr.Operand as FieldReference)?.FullName == "System.String Celeste.EventTrigger::Event" &&
                        instrs[instri + 1].OpCode.Name.ToLowerInvariant().StartsWith("stloc") &&
                        instrs[instri + 2].OpCode.Name.ToLowerInvariant().StartsWith("ldloc") &&
                        instrs[instri + 3].OpCode == OpCodes.Call && (instrs[instri + 3].Operand as MethodReference)?.GetID() == "System.UInt32 <PrivateImplementationDetails>::ComputeStringHash(System.String)"
                    ) {
                    // Insert a call to our own event handler here.
                    // If it returns true, return.

                    // Load "this" onto stack
                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));

                    //Load Player parameter onto stack
                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_1));

                    //Load Event field onto stack again
                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_Event));

                    // Call our static custom event handler.
                    instrs.Insert(instri++, il.Create(OpCodes.Call, m_TriggerCustomEvent));

                    // If we returned false, branch to ldfld. We still have the event ID on stack.
                    // This basically translates to if (result) { pop; ldstr ""; }; ldfld ...
                    instrs.Insert(instri, il.Create(OpCodes.Brfalse_S, instrs[instri]));
                    instri++;
                    // Otherwise, pop the event and return to skip any original event handler.
                    instrs.Insert(instri++, il.Create(OpCodes.Pop));
                    instrs.Insert(instri++, il.Create(OpCodes.Ret));
                }

                if (instr.OpCode == OpCodes.Ldstr) {
                    cctor_il.Emit(OpCodes.Dup);
                    cctor_il.Emit(OpCodes.Ldstr, instr.Operand);
                    cctor_il.Emit(OpCodes.Callvirt, m_LoadStrings_Add);
                    cctor_il.Emit(OpCodes.Pop); // HashSet.Add returns a bool.
                }

            }

            cctor_il.Emit(OpCodes.Stsfld, f_LoadStrings);
            cctor_il.Emit(OpCodes.Ret);
        }

        public static void PatchWaterUpdate(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            MethodReference m_Component_get_Entity = MonoModRule.Modder.Module.GetType("Monocle.Component").FindMethod("Monocle.Entity get_Entity()");
            if (m_Component_get_Entity == null)
                return;

            MethodReference m_WaterInteraction_get_Bounds = MonoModRule.Modder.Module.GetType("Celeste.WaterInteraction").FindProperty("Bounds").GetMethod;
            TypeReference t_Rectangle = m_WaterInteraction_get_Bounds.ReturnType;
            if (m_WaterInteraction_get_Bounds == null || t_Rectangle == null)
                return;

            VariableDefinition v_Bounds = new VariableDefinition(t_Rectangle);
            method.Body.Variables.Add(v_Bounds);

            MethodReference m_Entity_CollideCheck = MonoModRule.Modder.Module.GetType("Monocle.Entity").FindMethod($"System.Boolean CollideRect({t_Rectangle.FullName})");
            if (m_Entity_CollideCheck == null)
                return;

            MethodReference m_Rectangle_get_Center = MonoModRule.Modder.Module.ImportReference(t_Rectangle.Resolve().FindProperty("Center").GetMethod);
            if (m_Rectangle_get_Center == null)
                return;

            TypeReference t_Point = m_Rectangle_get_Center.ReturnType;
            FieldReference f_Point_Y = MonoModRule.Modder.Module.ImportReference(t_Point.Resolve().FindField("Y"));
            MethodReference m_Point_ToVector2 = MonoModRule.Modder.Module.GetType("Celeste.Mod.Extensions").FindMethod($"Microsoft.Xna.Framework.Vector2 Celeste.Mod.Extensions::ToVector2(Microsoft.Xna.Framework.Point)");
            if (t_Point == null || f_Point_Y == null || m_Point_ToVector2 == null)
                return;

            // Steam FNA is weird, and stores the entity in variable 5 instead of 3.
            bool isSteamFNA = method.Body.Variables[5].VariableType.FullName == "Monocle.Entity";

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                // If we have reached the end of the code to be patched, put everything back to normal.
                if (instrs[instri + 1].OpCode == OpCodes.Isinst && ((TypeReference) instrs[instri + 1].Operand).FullName == "Celeste.Player") {
                    instrs.Insert(instri++, isSteamFNA ? il.Create(OpCodes.Ldloc_S, (byte) 4) : il.Create(OpCodes.Ldloc_2));
                    instrs.Insert(instri++, il.Create(OpCodes.Callvirt, m_Component_get_Entity));
                    instrs.Insert(instri++, isSteamFNA ? il.Create(OpCodes.Stloc_S, (byte) 5) : il.Create(OpCodes.Stloc_3));
                    break;
                }

                // Load the WaterInteraction Bounds into a local variable
                if (instr.OpCode == OpCodes.Callvirt && ((MethodReference) instr.Operand).Name == "get_Entity") {
                    instrs[instri].Operand = m_WaterInteraction_get_Bounds;
                    instr.Operand = m_WaterInteraction_get_Bounds;
                    instrs[++instri].OpCode = OpCodes.Stloc_S;
                    instrs[instri].Operand = v_Bounds;
                }

                // Retrieve the Bounds instead of the entity
                if ((!isSteamFNA && instr.OpCode == OpCodes.Ldloc_3) || (isSteamFNA && instr.OpCode == OpCodes.Ldloc_S && ((VariableReference) instr.Operand).Index == 5)) {
                    if (instrs[instri + 1].OpCode == OpCodes.Callvirt && ((MethodReference) instrs[instri + 1].Operand).Name == "get_Center") {
                        instr.OpCode = OpCodes.Ldloca_S;
                        instr.Operand = v_Bounds;

                        if (instrs[instri + 2].OpCode == OpCodes.Ldfld && ((FieldReference) instrs[instri + 2].Operand).Name == "Y") {
                            // cast the Y position to float because it is compared to another float.
                            instrs.Insert(instri + 3, il.Create(OpCodes.Conv_R4));
                        }
                    } else {
                        instr.OpCode = OpCodes.Ldloc_S;
                        instr.Operand = v_Bounds;
                    }
                }

                // Replace any methods that use the entity
                if (instr.OpCode == OpCodes.Call && ((MethodReference) instr.Operand).FullName == "System.Boolean Monocle.Entity::CollideCheck(Monocle.Entity)")
                    instr.Operand = m_Entity_CollideCheck;

                if (instr.OpCode == OpCodes.Callvirt && ((MethodReference) instr.Operand).Name == "get_Center") {
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_Rectangle_get_Center;

                    Instruction next = instrs[++instri];
                    if (next.OpCode == OpCodes.Ldfld)
                        next.Operand = f_Point_Y;
                    else
                        instrs.Insert(instri, il.Create(OpCodes.Call, m_Point_ToVector2));
                }

                // Replace the Rectangle creation
                if (instr.OpCode == OpCodes.Newobj && ((MethodReference) instr.Operand).FullName == "System.Void Microsoft.Xna.Framework.Rectangle::.ctor(System.Int32,System.Int32,System.Int32,System.Int32)") {
                    instr.OpCode = OpCodes.Ldloc_S;
                    instr.Operand = v_Bounds;

                    instri--;

                    // Discard previous Rectangle args
                    while (instrs[instri].OpCode != OpCodes.Call || ((MethodReference) instrs[instri].Operand).FullName != "Monocle.Scene Monocle.Entity::get_Scene()") {
                        instrs.RemoveAt(instri);
                        instri--;
                    }
                }
            }
        }

        public static void PatchFakeHeartDialog(MethodDefinition method, CustomAttribute attrib) {
            FieldReference f_fakeHeartDialog = method.DeclaringType.FindField("fakeHeartDialog");
            FieldReference f_keepGoingDialog = method.DeclaringType.FindField("keepGoingDialog");
            if (f_fakeHeartDialog == null || f_keepGoingDialog == null)
                return;

            FieldDefinition f_this = null;

            // The routine is stored in a compiler-generated method.
            foreach (TypeDefinition nest in method.DeclaringType.NestedTypes) {
                if (!nest.Name.StartsWith("<" + method.Name + ">d__"))
                    continue;
                method = nest.FindMethod("System.Boolean MoveNext()") ?? method;
                f_this = method.DeclaringType.FindField("<>4__this");
                break;
            }

            if (!method.HasBody || f_this == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Ldstr) {
                    if (((string) instr.Operand) == "CH9_FAKE_HEART") {
                        instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                        instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_this));
                        instr.OpCode = OpCodes.Ldfld;
                        instr.Operand = f_fakeHeartDialog;

                    } else if (((string) instr.Operand) == "CH9_KEEP_GOING") {
                        instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                        instrs.Insert(instri, il.Create(OpCodes.Ldfld, f_this));
                        instr.OpCode = OpCodes.Ldfld;
                        instr.Operand = f_keepGoingDialog;
                    }
                }
            }
        }

        public static void PatchTextMenuOptionColor(MethodDefinition method, CustomAttribute attrib) {
            FieldReference f_UnselectedColor = method.DeclaringType.FindField("UnselectedColor");
            if (f_UnselectedColor == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Call && (instr.Operand as MethodReference)?.FullName == "Microsoft.Xna.Framework.Color Microsoft.Xna.Framework.Color::get_White()") {
                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instr.OpCode = OpCodes.Ldfld;
                    instr.Operand = f_UnselectedColor;
                }
            }
        }

        public static void PatchIntroCrusherSequence(MethodDefinition method, CustomAttribute attrib) {
            FieldReference f_triggered = method.DeclaringType.FindField("triggered");
            FieldReference f_manualTrigger = method.DeclaringType.FindField("manualTrigger");
            FieldReference f_delay = method.DeclaringType.FindField("delay");
            FieldReference f_speed = method.DeclaringType.FindField("speed");


            FieldReference f_this = null;

            // The gem collection routine is stored in a compiler-generated method.
            foreach (TypeDefinition nest in method.DeclaringType.NestedTypes) {
                if (!nest.Name.StartsWith("<" + method.Name + ">d__"))
                    continue;
                method = nest.FindMethod("System.Boolean MoveNext()") ?? method;
                f_this = method.DeclaringType.FindField("<>4__this");
                break;
            }

            if (!method.HasBody || f_this == null)
                return;

            // Steam FNA is weird, and all the local variables are messed up.
            bool isSteamFNA = method.Body.Variables[2].VariableType.FullName != "Celeste.Player";

            ILProcessor il = method.Body.GetILProcessor();
            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 1; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == (isSteamFNA ? OpCodes.Ldloc_1 : OpCodes.Ldloc_2) &&
                    instrs[instri + 1].OpCode == OpCodes.Brfalse_S) {
                    // Get the instruction at the beginning of this while loop.
                    Instruction loopTarget = (Instruction) instrs[instri + 1].Operand;
                    // Get the instruction after the end of this while statement
                    Instruction breakTarget = instrs.Last(i => i.Operand == loopTarget).Next;

                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_this));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_triggered));
                    instrs.Insert(instri++, il.Create(OpCodes.Brtrue_S, breakTarget));

                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_this));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_manualTrigger));
                    instrs.Insert(instri++, il.Create(OpCodes.Brtrue_S, loopTarget));
                }

                if (instr.OpCode == OpCodes.Ldloc_3 && // The value stored in loc_3 is different between versions, but it still works the same.
                    instrs[instri + 1].OpCode == OpCodes.Brfalse_S) {
                    // Get the instruction at the beginning of this while loop.
                    Instruction loopTarget = (Instruction) instrs[instri + 1].Operand;
                    // Get the instruction after the end of this while statement
                    Instruction breakTarget = instrs.Last(i => i.Operand == loopTarget).Next;

                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_this));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_manualTrigger));
                    instrs.Insert(instri++, il.Create(OpCodes.Brtrue_S, loopTarget));
                }

                // Allow for custom activation delay
                if (instr.OpCode == OpCodes.Ldc_R4 && ((float) instr.Operand) == 1.2f) {
                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                    instr.OpCode = OpCodes.Ldfld;
                    instr.Operand = f_this;
                    instrs.Insert(++instri, il.Create(OpCodes.Ldfld, f_delay));
                }

                // If the delay is less than, or equal to zero, don't add a shaker.
                if (instr.OpCode == OpCodes.Ldarg_0 &&
                    instrs[instri + 1].OpCode == OpCodes.Ldfld && ((FieldReference) instrs[instri + 1].Operand).Name == "<shaker>5__3" &&
                    instrs[instri + 2].OpCode == OpCodes.Callvirt && ((MethodReference) instrs[instri + 2].Operand).Name == "Add") {
                    Instruction breakTarget = instrs[instri + 3];

                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_delay));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldc_R4, 0f));
                    instrs.Insert(instri++, il.Create(OpCodes.Ble, breakTarget));

                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_this));
                }

                // Allow for custom movement speed
                if (instr.OpCode == OpCodes.Ldc_R4 && ((float) instr.Operand) == 2f &&
                    instrs[instri + 1].OpCode == OpCodes.Call && ((MethodReference) instrs[instri + 1].Operand).Name == "get_DeltaTime") {
                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_this));
                    instr.OpCode = OpCodes.Ldfld;
                    instr.Operand = f_speed;
                }
            }
        }

        public static void PatchOuiChapterPanelRender(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            MethodDefinition m_ModCardTexture = method.DeclaringType.FindMethod("System.String _ModCardTexture(System.String,Celeste.OuiChapterPanel)");
            if (m_ModCardTexture == null)
                return;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Ldstr && ((string) instr.Operand).StartsWith("areaselect/card")) {
                    // Move to after the string is loaded.
                    instri++;
                    // Push this.
                    instrs.Insert(instri, il.Create(OpCodes.Ldarg_0));
                    instri++;
                    // Insert method call to modify the string.
                    instrs.Insert(instri, il.Create(OpCodes.Call, m_ModCardTexture));
                    instri++;
                }
            }
        }

        public static void PatchGoldenBlockStaticMovers(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            MethodDefinition m_Platform_DisableStaticMovers = MonoModRule.Modder.Module.GetType("Celeste.Platform").FindMethod("System.Void DisableStaticMovers()");
            MethodDefinition m_Platform_DestroyStaticMovers = MonoModRule.Modder.Module.GetType("Celeste.Platform").FindMethod("System.Void DestroyStaticMovers()");

            ILProcessor il = method.Body.GetILProcessor();
            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 1; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Call && ((MethodReference) instr.Operand).Name == "Awake") {
                    instrs.Insert(++instri, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(++instri, il.Create(OpCodes.Call, m_Platform_DisableStaticMovers));
                }

                if (instr.OpCode == OpCodes.Call && ((MethodReference) instr.Operand).Name == "RemoveSelf") {
                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri++, il.Create(OpCodes.Call, m_Platform_DestroyStaticMovers));
                }
            }
        }

        public static void PatchCrushBlockFirstAlarm(MethodDefinition method) {
            if (method?.Body == null)
                return;

            ILProcessor il = method.Body.GetILProcessor();
            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;

            Instruction instrPop = null;
            for (int instri = 1; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instrs[instri - 1].OpCode == OpCodes.Callvirt && (instrs[instri - 1].Operand as MethodReference)?.GetID() == "Celeste.SoundSource Celeste.SoundSource::Stop(System.Boolean)" &&
                    instr.OpCode == OpCodes.Pop) {
                    instrPop = instr;
                    break;
                }
            }

            if (instrPop == null)
                return;

            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Ldfld && (instr.Operand as FieldReference)?.FullName == "Celeste.SoundSource Celeste.CrushBlock::currentMoveLoopSfx") {
                    instri++;

                    instrs.Insert(instri, il.Create(OpCodes.Dup));
                    instri++;

                    instrs.Insert(instri, il.Create(OpCodes.Brfalse, instrPop));
                    instri++;
                }
            }
        }

        public static void PatchLookoutUpdate(MethodDefinition method, CustomAttribute attrib) {
            if (method?.Body == null)
                return;

            const string lookoutTalkFieldName = "Celeste.TalkComponent Celeste.Lookout::talk";
            const string entityCollideCheckMethodID = "System.Boolean Monocle.Entity::CollideCheck<Celeste.Solid>()";

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;

            // if (this.talk == null || !CollideCheck<Solid>())  <-- indexTarget is here
            //     return;
            // this.Remove((Component) this.talk);
            // this.talk = (TalkComponent) null;

            int indexTarget = -1;
            MethodReference collideCheckMethodReference = null;
            for (int instri = 0; instri < instrs.Count - 5; instri++) {
                if (instrs[instri].OpCode == OpCodes.Ldarg_0
                    && instrs[instri + 1].OpCode == OpCodes.Ldfld && (instrs[instri + 1].Operand as FieldReference)?.FullName == lookoutTalkFieldName
                    && instrs[instri + 2].OpCode == OpCodes.Brfalse_S
                    && instrs[instri + 3].OpCode == OpCodes.Ldarg_0
                    && instrs[instri + 4].OpCode == OpCodes.Call && (instrs[instri + 4].Operand as MethodReference)?.GetID() == entityCollideCheckMethodID
                    && (instrs[instri + 5].OpCode == OpCodes.Brfalse_S || instrs[instri + 5].OpCode == OpCodes.Br_S)
                ) {
                    indexTarget = instri;
                    collideCheckMethodReference = instrs[instri + 4].Operand as MethodReference;
                    break;
                }
            }

            if (indexTarget < 0)
                return;

            // if (true || !CollideCheck<Solid>())  <-- after patch
            //     return;
            // this.Remove((Component) this.talk);
            // this.talk = (TalkComponent) null;
            ILProcessor il = method.Body.GetILProcessor();
            instrs[indexTarget].OpCode = OpCodes.Nop;
            instrs[indexTarget + 1] = il.Create(OpCodes.Ldc_I4_0);

            // insert at begin
            // if (talk.UI != null) {
            //     talk.UI.Visible = !CollideCheck<Solid>();
            // }
            int indexInsert = 0;
            Instruction startIns = instrs[0];

            instrs.Insert(indexInsert, il.Create(OpCodes.Ldarg_0));
            instrs.Insert(++indexInsert, il.Create(OpCodes.Ldfld, method.DeclaringType.FindField("talk")));
            instrs.Insert(++indexInsert, il.Create(OpCodes.Ldfld, method.Module.GetType("Celeste.TalkComponent").FindField("UI")));
            instrs.Insert(++indexInsert, il.Create(OpCodes.Brfalse_S, startIns));

            instrs.Insert(++indexInsert, il.Create(OpCodes.Ldarg_0));
            instrs.Insert(++indexInsert, il.Create(OpCodes.Ldfld, method.DeclaringType.FindField("talk")));
            instrs.Insert(++indexInsert, il.Create(OpCodes.Ldfld, method.Module.GetType("Celeste.TalkComponent").FindField("UI")));
            instrs.Insert(++indexInsert, il.Create(OpCodes.Ldarg_0));
            instrs.Insert(++indexInsert, il.Create(OpCodes.Call, collideCheckMethodReference));
            instrs.Insert(++indexInsert, il.Create(OpCodes.Ldc_I4_0));
            instrs.Insert(++indexInsert, il.Create(OpCodes.Ceq));
            instrs.Insert(++indexInsert, il.Create(OpCodes.Stfld, method.Module.GetType("Monocle.Entity").FindField("Visible")));
        }

        public static void PatchDecalUpdate(MethodDefinition method, CustomAttribute attrib) {
            if (!method.HasBody)
                return;

            FieldDefinition f_hideRange = method.DeclaringType.FindField("hideRange");
            FieldDefinition f_showRange = method.DeclaringType.FindField("showRange");

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Ldc_R4 && ((float) instr.Operand) == 32f) {
                    instrs.RemoveAt(instri);
                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_hideRange));
                }

                if (instr.OpCode == OpCodes.Ldc_R4 && ((float) instr.Operand) == 48f) {
                    instrs.RemoveAt(instri);
                    instrs.Insert(instri++, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(instri++, il.Create(OpCodes.Ldfld, f_showRange));
                }
            }
        }

        public static void PatchAreaCompleteMusic(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_playCustomCompleteScreenMusic = method.DeclaringType.FindMethod("System.Boolean playCustomCompleteScreenMusic()");

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            int injectionPoint = -1;
            for (int instri = 0; instri < instrs.Count - 2; instri++) {
                if (instrs[instri].OpCode == OpCodes.Call && (instrs[instri].Operand as MethodReference).DeclaringType.Name == "RunThread") {
                    injectionPoint = instri + 1;
                }

                if (injectionPoint != -1
                    && instrs[instri].OpCode == OpCodes.Ldnull
                    && instrs[instri + 1].OpCode == OpCodes.Ldc_I4_1
                    && instrs[instri + 2].OpCode == OpCodes.Call && (instrs[instri + 2].Operand as MethodReference).Name == "SetAmbience") {

                    // we want to inject code just after RunThread.Start (which position was saved earlier) that calls playCustomCompleteScreenMusic(),
                    // and sends execution to Audio.SetAmbience(null) if it returned true (skipping over the vanilla code playing endscreen music).

                    Instruction target = instrs[instri];

                    instrs.Insert(injectionPoint++, il.Create(OpCodes.Ldarg_0));
                    instrs.Insert(injectionPoint++, il.Create(OpCodes.Call, m_playCustomCompleteScreenMusic));
                    instrs.Insert(injectionPoint++, il.Create(OpCodes.Brtrue_S, target));
                    break;
                }
            }
        }

        public static void PatchAreaCompleteVersionNumberAndVariants(ILContext il, CustomAttribute attrib) {
            ILCursor c = new ILCursor(il);
            c.GotoNext(MoveType.After, instr => instr.MatchLdcR4(1020f));
            c.Emit(OpCodes.Ldsfld, il.Method.DeclaringType.FindField("versionOffset"));
            c.Emit(OpCodes.Add);
        }

        public static void PatchInputConfigReset(ILContext il, CustomAttribute attrib) {
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
        }

        public static void PatchAscendManagerRoutine(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition routine = method;

            // The routine is stored in a compiler-generated method.
            foreach (TypeDefinition nest in method.DeclaringType.NestedTypes) {
                if (!nest.Name.StartsWith("<" + method.Name + ">d__")) {
                    continue;
                }
                routine = nest.FindMethod("System.Boolean MoveNext()") ?? method;
                break;
            }

            TypeDefinition t_Vector2 = MonoModRule.Modder.FindType("Microsoft.Xna.Framework.Vector2").Resolve();

            MethodDefinition m_ShouldRestorePlayerX = method.DeclaringType.FindMethod("System.Boolean ShouldRestorePlayerX()");
            MethodDefinition m_Entity_set_X = method.Module.GetType("Monocle.Entity").FindMethod("System.Void set_X(System.Single)").Resolve();

            FieldDefinition f_this = routine.DeclaringType.FindField("<>4__this");
            FieldDefinition f_player = routine.DeclaringType.Fields.FirstOrDefault(f => f.Name.StartsWith("<player>5__"));
            FieldDefinition f_from = routine.DeclaringType.Fields.FirstOrDefault(f => f.Name.StartsWith("<from>5__"));
            FieldReference f_Vector2_X = MonoModRule.Modder.Module.ImportReference(t_Vector2.FindField("X"));

            ILCursor cursor = new ILCursor(new ILContext(routine));

            // move after this.Scene.Add(fader)
            cursor.GotoNext(MoveType.After,
                instr => instr.MatchLdarg(0),
                instr => instr.OpCode == OpCodes.Ldfld && ((FieldReference) instr.Operand).Name.StartsWith("<fader>5__"),
                instr => instr.OpCode == OpCodes.Callvirt && ((MethodReference) instr.Operand).GetID() == "System.Void Monocle.Scene::Add(Monocle.Entity)");

            // target: from = player.Position;
            Instruction target = cursor.Next;

            // _ = this.ShouldRestorePlayerX();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_this);
            cursor.Emit(OpCodes.Call, m_ShouldRestorePlayerX);

            // if (!_) goto target;
            cursor.Emit(OpCodes.Brfalse, target);

            // player.X = from.X;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_player);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldflda, f_from);
            cursor.Emit(OpCodes.Ldfld, f_Vector2_X);
            cursor.Emit(OpCodes.Callvirt, m_Entity_set_X);
        }

        public static void PatchCommandsUpdateOpen(ILContext il, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(il);

            TypeDefinition t_Engine = MonoModRule.Modder.FindType("Monocle.Engine").Resolve();
            MethodReference m_get_RawDeltaTime = t_Engine.FindMethod("System.Single get_RawDeltaTime()");

            while (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCall("Monocle.Engine", "get_DeltaTime"))) {
                cursor.Next.Operand = m_get_RawDeltaTime;
            }
        }

        public static void ForceName(ICustomAttributeProvider cap, CustomAttribute attrib) {
            if (cap is IMemberDefinition member)
                member.Name = (string) attrib.ConstructorArguments[0].Value;
        }

        public static void PatchOuiFileSelectEnter(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition routine = method;

            // The routine is stored in a compiler-generated method.
            string methodName = method.Name;
            if (methodName.StartsWith("orig_")) {
                methodName = methodName.Substring(5);
            }
            foreach (TypeDefinition nest in method.DeclaringType.NestedTypes) {
                if (!nest.Name.StartsWith("<" + methodName + ">d__")) {
                    continue;
                }
                routine = nest.FindMethod("System.Boolean MoveNext()") ?? method;
                break;
            }

            MethodDefinition m_RemoveSlotsFromScene = method.DeclaringType.FindMethod("System.Void RemoveSlotsFromScene()");
            MethodDefinition m_AddSlotsToScene = method.DeclaringType.FindMethod("System.Void AddSlotsToScene()");

            FieldDefinition f_this = routine.DeclaringType.FindField("<>4__this");

            ILCursor cursor = new ILCursor(new ILContext(routine));

            cursor.GotoNext(MoveType.After,
                instr => instr.MatchLdsfld("Celeste.OuiFileSelect", "Loaded"));

            // Steam FNA is weird
            bool isSteamFNA = cursor.Next.MatchLdcI4(0);

            // remove slots from scene in main thread before RunThread.Start(this.LoadThread, "FILE_LOADING", false)
            if (isSteamFNA) {
                cursor.GotoNext(MoveType.Before,
                    instr => instr.MatchLdarg(0),
                    instr => instr.OpCode == OpCodes.Ldfld && ((FieldReference) instr.Operand).Name == "<>4__this",
                    instr => instr.OpCode == OpCodes.Ldftn && ((MethodReference) instr.Operand).GetID() == "System.Void Celeste.OuiFileSelect::LoadThread()");
            } else {
                cursor.GotoNext(MoveType.Before,
                    instr => instr.MatchLdloc(out int _),
                    instr => instr.OpCode == OpCodes.Ldftn && ((MethodReference) instr.Operand).GetID() == "System.Void Celeste.OuiFileSelect::LoadThread()");
            }

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_this);
            cursor.Emit(OpCodes.Call, m_RemoveSlotsFromScene);

            // add slots to scene in main thread after saves are loaded
            if (isSteamFNA) {
                cursor.GotoNext(MoveType.Before,
                    instr => instr.MatchLdarg(0),
                    instr => instr.OpCode == OpCodes.Ldfld && ((FieldReference) instr.Operand).Name == "<>4__this",
                    instr => instr.MatchLdfld("Celeste.OuiFileSelect", "loadedSuccess"));
            } else {
                cursor.GotoNext(MoveType.Before,
                    instr => instr.MatchLdloc(out int _),
                    instr => instr.MatchLdfld("Celeste.OuiFileSelect", "loadedSuccess"));
            }

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, f_this);
            cursor.Emit(OpCodes.Call, m_AddSlotsToScene);
        }

        public static void PatchOuiFileSelectLoadThread(ILContext il, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(il);

            // remove removing slots from scene in loading thread
            cursor.GotoNext(MoveType.AfterLabel,
                instr => instr.MatchLdarg(0),
                instr => instr.MatchLdfld("Celeste.OuiFileSelect", "Slots"),
                instr => instr.MatchLdloc(out int _),
                instr => instr.MatchLdelemRef());

            int startIndex = cursor.Index;

            ILCursor cursorTemp = cursor.Clone();
            cursorTemp.GotoNext(MoveType.After,
                instr => instr.OpCode == OpCodes.Callvirt && ((MethodReference) instr.Operand).GetID() == "System.Void Monocle.Scene::Remove(Monocle.Entity)");

            cursor.RemoveRange(cursorTemp.Index - startIndex);

            // remove adding slots to scene in loading thread
            cursor.GotoNext(MoveType.AfterLabel,
                instr => instr.MatchLdarg(0),
                instr => instr.OpCode == OpCodes.Call && ((MethodReference) instr.Operand).GetID() == "Monocle.Scene Monocle.Entity::get_Scene()",
                instr => instr.MatchLdloc(out int _),
                instr => instr.OpCode == OpCodes.Callvirt && ((MethodReference) instr.Operand).GetID() == "System.Void Monocle.Scene::Add(Monocle.Entity)");

            cursor.RemoveRange(4);
        }

        public static void PatchCoroutineHackfixHelperStateMachine(ILContext il, CustomAttribute attrib) {
            // The CoroutineDelayHackfixHelper Wrap coroutine is stored in a compiler-generated method.
            TypeDefinition coroutineHackfixWrapCoroutineType = null;
            foreach (TypeDefinition nest in Celeste.Module.GetType("Celeste.Mod.Helpers.CoroutineDelayHackfixHelper").NestedTypes) {
                if (!nest.Name.StartsWith("<Wrap>d__")) {
                    continue;
                }
                coroutineHackfixWrapCoroutineType = nest;
                break;
            }

            ILCursor cursor = new ILCursor(il);
            while (cursor.TryGotoNext(instr => instr.MatchLdtoken(Celeste.Module.GetType("Celeste.Mod.Helpers.CoroutineDelayHackfixHelper")))) {
                cursor.Next.Operand = coroutineHackfixWrapCoroutineType;
            }
        }

        public static void PostProcessor(MonoModder modder) {
            // Patch CrushBlock::AttackSequence's first alarm delegate manually because how would you even annotate it?
            PatchCrushBlockFirstAlarm(modder.Module.GetType("Celeste.CrushBlock/<>c__DisplayClass41_0")?.FindMethod("<AttackSequence>b__1"));

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
                method.FixShortLongOps();
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
