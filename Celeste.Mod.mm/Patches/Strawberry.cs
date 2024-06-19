#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    [PatchStrawberryInterface]
    class patch_Strawberry : Strawberry {

        public patch_Strawberry(EntityData data, Vector2 offset, EntityID gid)
            : base(data, offset, gid) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_OnCollect();
        [PatchInterface]
        public new void OnCollect() {
            orig_OnCollect();
            // "Patch hook", because maintaining a pre-Everest MMHOOK is too much work.
            Everest.DiscordSDK.Instance?.UpdatePresence((Scene as Level)?.Session);
        }

        [MonoModIgnore]
        [PatchStrawberryTrainCollectionOrder]
        public extern void orig_Update();

        public new void Update() {
            orig_Update();
        }

        // Patch interface-implemented methods
        [MonoModIgnore]
        [PatchInterface]
        public extern new void CollectedSeeds();
    }
}

namespace MonoMod {
    /// <summary>
    /// A patch for Strawberry that takes into account that some modded strawberries may not allow standard collection rules.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchStrawberryTrainCollectionOrder))]
    class PatchStrawberryTrainCollectionOrder : Attribute { }

    /// <summary>
    /// Patch the Strawberry class to tack on the IStrawberry interface for the StrawberryRegistry
    /// </summary>
    [MonoModCustomAttribute(nameof(MonoModRules.PatchStrawberryInterface))]
    class PatchStrawberryInterfaceAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchStrawberryTrainCollectionOrder(MethodDefinition method, CustomAttribute attrib) {
            TypeDefinition t_StrawberryRegistry = MonoModRule.Modder.FindType("Celeste.Mod.StrawberryRegistry")?.Resolve();
            MethodDefinition m_IsFirst = t_StrawberryRegistry.FindMethod("System.Boolean IsFirstStrawberry(Monocle.Entity)");

            bool found = false;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                // Rip out the vanilla code call and replace it with vanilla-considerate code
                if (instr.MatchCallvirt("Celeste.Strawberry", "get_IsFirstStrawberry")) {
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_IsFirst;
                    instri++;
                    found = true;
                }
            }

            if (!found) {
                throw new Exception("Call to Strawberry.get_IsFirstStrawberry not found in " + method.FullName + "!");
            }
        }

        public static void PatchStrawberryInterface(ICustomAttributeProvider provider, CustomAttribute attrib) {
            // MonoModRule.Modder.FindType("Celeste.Mod.IStrawberry");
            InterfaceImplementation i_IStrawberry = new InterfaceImplementation(MonoModRule.Modder.FindType("Celeste.Mod.IStrawberry"));

            ((TypeDefinition) provider).Interfaces.Add(i_IStrawberry);
        }

    }
}
