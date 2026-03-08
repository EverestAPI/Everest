using Celeste.Mod;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;
using System.Collections.Generic;

namespace Celeste {
    class patch_InteractTrigger : InteractTrigger {

        private static HashSet<string> _LoadStrings; // generated in MonoModRules.PatchInteractTriggerOnTalk

        public delegate Entity InteractLoader(InteractTrigger trigger, Player player, string eventID, ref bool progressEvent);
        public static readonly Dictionary<string, InteractLoader> InteractLoaders = new();

        // MonoMod ignores this - this is only required to compile
        public patch_InteractTrigger(EntityData data, Vector2 offset) : base(data, offset) { }

        [MonoModIgnore] // don't change anything about this method...
        [PatchInteractTriggerOnTalk] // ... except for injecting code into it via a MonoModRules patch.
        public extern new void OnTalk(Player player);

        public static bool TriggerCustomInteract(InteractTrigger trigger, Player player, string eventID, ref bool progressEvent) {
            if (string.IsNullOrEmpty(eventID))
                return false;

            if (Everest.Events.InteractTrigger.TriggerInteract(trigger, player, eventID, ref progressEvent))
                return true;

            if (InteractLoaders.TryGetValue(eventID, out InteractLoader loader)) {
                Entity loaded = loader(trigger, player, eventID, ref progressEvent);
                if (loaded != null) {
                    trigger.Scene.Add(loaded);
                    return true;
                }
            }

            if (!_LoadStrings.Contains(eventID)) {
                Logger.Warn("InteractTrigger", $"Interact Event '{eventID}' does not exist!");
            }

            return false;
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Include a check for custom interact events.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchInteractTriggerOnTalk))]
    class PatchInteractTriggerOnTalkAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchInteractTriggerOnTalk(MethodDefinition method, CustomAttribute attr) {
            // we're also going to patch the static constructor from here.
            MethodDefinition m_cctor = method.DeclaringType.FindMethod(".cctor");
            FieldDefinition f_LoadStrings = method.DeclaringType.FindField("_LoadStrings");

            MethodDefinition m_TriggerCustomInteract = method.DeclaringType.FindMethod("System.Boolean TriggerCustomInteract(Celeste.InteractTrigger,Celeste.Player,System.String,System.Boolean&)");

            Mono.Collections.Generic.Collection<Instruction> cctor_instrs = m_cctor.Body.Instructions;
            ILProcessor cctor_il = m_cctor.Body.GetILProcessor();

            // the `ret` at the end of the cctor will be added again later
            cctor_il.RemoveAt(cctor_instrs.Count - 1);

            TypeDefinition td_LoadStrings = f_LoadStrings.FieldType.Resolve();
            MethodReference m_LoadStrings_Add = MonoModRule.Modder.Module.ImportReference(td_LoadStrings.FindMethod("Add"));
            m_LoadStrings_Add.DeclaringType = f_LoadStrings.FieldType;
            MethodReference m_LoadStrings_ctor = MonoModRule.Modder.Module.ImportReference(td_LoadStrings.FindMethod("System.Void .ctor()"));
            m_LoadStrings_ctor.DeclaringType = f_LoadStrings.FieldType;

            // before we get the strings, we must first construct a new HashSet
            cctor_il.Emit(OpCodes.Newobj, m_LoadStrings_ctor);

            bool eventHandlerInjectionPointFound = false;
            bool loadStringFound = false;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();

            int loc_bool_flag = -1;
            int loc_string_eventID = -1;
            object afterSwitchLabel = null;

            for (int i = 0; i < instrs.Count; i++) {
                Instruction instr = instrs[i];

                /*
                 Plan:
                 
                 bool flag = true;
                 string eventID = Events[eventIndex]; // the IL code actually stores this into a local.
                  <-- if (TriggerCustomInteract(this, player, eventID, ref flag)) // point of injection here.
                  <--     goto afterSwitch; // if true, branch past the switch block.
                 switch (eventID) { [...] }
                  <-- afterSwitch:
                 
                 Expected IL:
                 
                 ldc.i4.1
                 stloc.0   // local bool flag
                 ldarg.0
                 ldfld    class [mscorlib]System.Collections.Generic.List`1<string> Celeste.InteractTrigger::Events
                 ldarg.0
                 ldfld    int32 Celeste.InteractTrigger::eventIndex
                 callvirt instance !0 class [mscorlib]System.Collections.Generic.List`1<string>::get_Item(int32)
                 stloc.1   // local string eventID
                  <------- // we inject our instructions here.
                 ldloc.1   // the switch block begins here
                 ...
                 br        // to an address just after the switch block
                 */

                // we only need to inject once
                if (!eventHandlerInjectionPointFound) {
                    bool flagFound = loc_bool_flag != -1;
                    bool eventIDFound = loc_string_eventID != -1;

                    // first, we find our two locals
                    if (!flagFound
                        && i < instrs.Count - 1
                        && instr.MatchLdcI4(1)
                        && instrs[i + 1].MatchStloc(out loc_bool_flag)) {
                        flagFound = true;
                    }
                    if (!eventIDFound
                        && i < instrs.Count - 2
                        && instr.MatchLdfld("Celeste.InteractTrigger", "eventIndex")
                        && instrs[i + 1].MatchCallvirt("System.Collections.Generic.List`1<System.String>", "get_Item")
                        && instrs[i + 2].MatchStloc(out loc_string_eventID)) {
                        eventIDFound = true;
                    }

                    if (flagFound && eventIDFound
                        && i >= 1
                        && instrs[i - 1].MatchStloc(loc_string_eventID)) {

                        // any `br` after this point leads past the switch block
                        for (int j = i; j < instrs.Count; j++) {
                            if (instrs[j].OpCode == OpCodes.Br) {
                                afterSwitchLabel = instrs[j].Operand;
                                break;
                            }
                        }

                        if (afterSwitchLabel is not null) {
                            // we're ready to inject!

                            instrs.Insert(i++, il.Create(OpCodes.Ldarg_0)); // `this` onto stack
                            instrs.Insert(i++, il.Create(OpCodes.Ldarg_1)); // parameter `player` onto stack
                            instrs.Insert(i++, CreateLdlocS(il, (byte) loc_string_eventID)); // local `eventID` onto stack
                            instrs.Insert(i++, il.Create(OpCodes.Ldloca_S, (byte) loc_bool_flag)); // ref local `flag` onto stack

                            instrs.Insert(i++, il.Create(OpCodes.Call, m_TriggerCustomInteract)); // call our static handler

                            instrs.Insert(i++, il.Create(OpCodes.Brtrue, afterSwitchLabel)); // branch to label if true

                            eventHandlerInjectionPointFound = true;

                            static Instruction CreateLdlocS(ILProcessor il, byte index) {
                                return index switch {
                                    0 => il.Create(OpCodes.Ldloc_0),
                                    1 => il.Create(OpCodes.Ldloc_1),
                                    2 => il.Create(OpCodes.Ldloc_2),
                                    3 => il.Create(OpCodes.Ldloc_3),
                                    _ => il.Create(OpCodes.Ldloc_S, index)
                                };
                            }
                        }
                    }
                }

                // we've found a string. record it into the HashSet.
                if (instr.OpCode == OpCodes.Ldstr) {
                    cctor_il.Emit(OpCodes.Dup);
                    cctor_il.Emit(OpCodes.Ldstr, instr.Operand);
                    cctor_il.Emit(OpCodes.Callvirt, m_LoadStrings_Add);
                    cctor_il.Emit(OpCodes.Pop); // HashSet.Add returns a bool. discard it.

                    loadStringFound = true;
                }
            }

            if (!eventHandlerInjectionPointFound) {
                throw new Exception("Event handler injection point not found in " + method.FullName + "!");
            }
            if (!loadStringFound) {
                throw new Exception("ldstr not found in " + method.FullName + "!");
            }

            cctor_il.Emit(OpCodes.Stsfld, f_LoadStrings);
            cctor_il.Emit(OpCodes.Ret);
        }

    }
}
