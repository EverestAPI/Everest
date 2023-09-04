#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using System;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    class patch_EventTrigger : EventTrigger {

        private static HashSet<string> _LoadStrings; // Generated in MonoModRules.PatchEventTriggerOnEnter

        public delegate Entity CutsceneLoader(EventTrigger trigger, Player player, string eventID);
        public static readonly Dictionary<string, CutsceneLoader> CutsceneLoaders = new Dictionary<string, CutsceneLoader>();

        private patch_EventTrigger(EntityData data, Vector2 offset) : base(data, offset) {
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchEventTriggerOnEnter] // ... except for manually manipulating the method via MonoModRules
        public override extern void OnEnter(Player player);

        public static bool TriggerCustomEvent(EventTrigger trigger, Player player, string eventID) {
            if (Everest.Events.EventTrigger.TriggerEvent(trigger, player, eventID))
                return true;

            if (CutsceneLoaders.TryGetValue(eventID, out CutsceneLoader loader)) {
                Entity loaded = loader(trigger, player, eventID);
                if (loaded != null) {
                    trigger.Scene.Add(loaded);
                    return true;
                }
            }

            if (!_LoadStrings.Contains(eventID)) {
                Logger.Warn("EventTrigger", $"Event '{eventID}' does not exist!");
                return true; //To a avoid hard crash on missing event
            }

            return false;
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Include check for custom events.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchEventTriggerOnEnter))]
    class PatchEventTriggerOnEnterAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchEventTriggerOnEnter(MethodDefinition method, CustomAttribute attrib) {
            // We also need to do special work in the cctor.
            MethodDefinition m_cctor = method.DeclaringType.FindMethod(".cctor");

            MethodDefinition m_TriggerCustomEvent = method.DeclaringType.FindMethod("System.Boolean TriggerCustomEvent(Celeste.EventTrigger,Celeste.Player,System.String)");

            FieldDefinition f_Event = method.DeclaringType.FindField("Event");

            FieldDefinition f_LoadStrings = method.DeclaringType.FindField("_LoadStrings");

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

            bool eventHandlerInjectionPointFound = false;
            bool loadStringFound = false;

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
                    instri < instrs.Count - 3 &&
                    instr.MatchLdfld("Celeste.EventTrigger", "Event") &&
                    instrs[instri + 1].MatchStloc(out int _) &&
                    instrs[instri + 2].MatchLdloc(out int _) &&
                    instrs[instri + 3].MatchCall("<PrivateImplementationDetails>", "ComputeStringHash")
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

                    eventHandlerInjectionPointFound = true;
                }

                if (instr.OpCode == OpCodes.Ldstr) {
                    cctor_il.Emit(OpCodes.Dup);
                    cctor_il.Emit(OpCodes.Ldstr, instr.Operand);
                    cctor_il.Emit(OpCodes.Callvirt, m_LoadStrings_Add);
                    cctor_il.Emit(OpCodes.Pop); // HashSet.Add returns a bool.

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
