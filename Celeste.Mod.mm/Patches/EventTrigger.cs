using System;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_EventTrigger : EventTrigger {
        // cutscene loaders for custom events
        public delegate Entity CutsceneLoader(EventTrigger trigger, Player player, string eventID);
        public static readonly Dictionary<string, CutsceneLoader> CutsceneLoaders = new();

        // whether to use a `TalkComponent` to trigger the cutscene instead
        private bool useInteract;
        // whether `OnEnter` was called via the `TalkComponent` callback
        private bool interactTriggered;
        // the `TalkComponent` itself
        private TalkComponent talkComponent;

        // a flag which, when enabled, prevents this trigger from loading
        private string deleteFlag;

        private patch_EventTrigger(EntityData data, Vector2 offset) : base(data, offset) { }
        
        // patch the constructor to set `useInteract` and `deleteFlag` and add the `TalkComponent` if necessary
        public extern void orig_ctor(EntityData data, Vector2 offset);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset) {
            orig_ctor(data, offset);

            useInteract = data.Bool("useInteract");
            if (useInteract)
                Add(talkComponent = new TalkComponent(
                        new Rectangle(0, 0, (int) Width, (int) Height),
                        (data.FirstNodeNullable(offset) ?? Center) - Position,
                        player => {
                            interactTriggered = true;
                            OnEnter(player);
                        }
                    ) {
                        PlayerMustBeFacing = false
                    });

            deleteFlag = data.Attr("deleteFlag");
        }

        // remove ourselves if the flag with name `deleteFlag` is set
        public override void Added(Scene scene) {
            base.Added(scene);

            if (!string.IsNullOrEmpty(deleteFlag)
                && SceneAs<Level>().Session.GetFlag(deleteFlag))
                RemoveSelf();
        }
        
        // patch `Awake` so `OnSpawnHack` is ignored if `useInteract` is enabled
        [MonoModIgnore]
        [PatchEventTriggerAwake]
        public override extern void Awake(Scene scene);

        // patch `OnEnter` to allow loading custom cutscenes
        [MonoModIgnore]
        [PatchEventTriggerOnEnter]
        public override extern void OnEnter(Player player);

        // loads + adds a custom cutscene with a given ID to the scene
        public static void TriggerCustomEvent(EventTrigger trigger, Player player, string eventID) {
            if (Everest.Events.EventTrigger.TriggerEvent(trigger, player, eventID))
                return;

            if (CutsceneLoaders.TryGetValue(eventID, out CutsceneLoader loader)
                && loader(trigger, player, eventID) is { } loaded) {
                trigger.Scene.Add(loaded);
                return;
            }
            
            Logger.Warn("EventTrigger", $"Event '{eventID}' does not exist!");
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Make `OnSpawnHack` respect `useInteract`.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchEventTriggerAwake))]
    class PatchEventTriggerAwakeAttribute : Attribute { }
    
    /// <summary>
    /// Include check for custom events.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchEventTriggerOnEnter))]
    class PatchEventTriggerOnEnterAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchEventTriggerAwake(MethodDefinition method, CustomAttribute _) {
            // we want to add a `&& !this.useInteract` to the check for `this.OnSpawnHack`
            
            FieldDefinition f_useInteract = method.DeclaringType.FindField("useInteract")!;

            new ILContext(method).Invoke(il => {
                ILCursor cursor = new(il);
                
                // add a `&& !this.useInteract` to the check for `this.OnSpawnHack`
                ILLabel afterTrigger = null;
                cursor.GotoNext(MoveType.After,
                    instr => instr.MatchLdfld("Celeste.EventTrigger", "OnSpawnHack"),
                    instr => instr.MatchBrfalse(out afterTrigger));

                // emit `&& !this.useInteract`
                cursor.EmitLdarg0();
                cursor.EmitLdfld(f_useInteract); // this.useInteract
                cursor.EmitBrtrue(afterTrigger!);
            });
        }
        
        public static void PatchEventTriggerOnEnter(MethodDefinition method, CustomAttribute _) {
            /*
             * we want to:
             * 1. add a `|| (this.useInteract && !this.interactTriggered)` to the check for `this.triggered`
             * 2. set `this.talkComponent.Enabled = false;` when setting `this.triggered = true;`
             * 3. replace the throw in the default case of the switch statement with a call to `TriggerCustomEvent`
             */
            
            MethodDefinition m_TriggerCustomEvent = method.DeclaringType.FindMethod("System.Void TriggerCustomEvent(Celeste.EventTrigger,Celeste.Player,System.String)")!;

            FieldDefinition f_useInteract = method.DeclaringType.FindField("useInteract")!;
            FieldDefinition f_interactTriggered = method.DeclaringType.FindField("interactTriggered")!;
            FieldDefinition f_talkComponent = method.DeclaringType.FindField("talkComponent")!;
            FieldDefinition f_Event = method.DeclaringType.FindField("Event")!;

            FieldDefinition f_TalkComponent_Enabled = method.Module.GetType("Celeste.TalkComponent").FindField("Enabled")!;

            new ILContext(method).Invoke(il => {
                ILCursor cursor = new(il);
                
                // 1. add a `|| (this.useInteract && !this.interactTriggered)` to the check for `this.triggered`
                cursor.GotoNext(MoveType.After, instr => instr.MatchLdfld("Celeste.EventTrigger", "triggered"));
                
                // retrieve labels pointing to the `ret` and after it
                ILLabel ret = cursor.DefineLabel(), afterRet = cursor.DefineLabel();
                Instruction retInstr = cursor.Clone().GotoNext(instr => instr.MatchRet()).Next!;
                ret.Target = retInstr;
                afterRet.Target = retInstr.Next!;
                
                // remove `brfalse.s`
                cursor.Remove();
                
                // emit `this.triggered || (this.useInteract && !this.interactTriggered)`
                // `this.triggered` already on the stack
                cursor.EmitBrtrue(ret);
                
                cursor.EmitLdarg0();
                cursor.EmitLdfld(f_useInteract); // `this.useInteract`
                cursor.EmitBrfalse(afterRet);
                
                cursor.EmitLdarg0();
                cursor.EmitLdfld(f_interactTriggered); // `this.interactTriggered`
                cursor.EmitBrtrue(afterRet);
                
                // 2. set `this.talkComponent.Enabled = false;` when setting `this.triggered = true;`
                cursor.GotoNext(MoveType.After, instr => instr.MatchStfld("Celeste.EventTrigger", "triggered"));

                // emit `this.talkComponent.Enabled = false;`
                cursor.EmitLdarg0();
                cursor.EmitLdfld(f_talkComponent); // `this.talkComponent`
                cursor.EmitLdcI4(0); // `false`
                cursor.EmitStfld(f_TalkComponent_Enabled);
                
                // 3. replace the throw in the default case of the switch statement with a call to `TriggerCustomEvent`
                cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdstr("Event '"));
                
                // remove throw
                cursor.RemoveRange(7);
                
                // emit call to `TriggerCustomEvent`
                cursor.EmitLdarg0(); // `this`
                cursor.EmitLdarg1(); // `player`
                cursor.EmitLdarg0();
                cursor.EmitLdfld(f_Event); // `this.Event`
                cursor.EmitCall(m_TriggerCustomEvent);
                
                // emit a `ret` to end the switch case
                cursor.EmitRet();
            });
        }
    }
}