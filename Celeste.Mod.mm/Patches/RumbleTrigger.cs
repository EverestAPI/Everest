#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Celeste {
    class patch_RumbleTrigger : RumbleTrigger {

        private bool manualTrigger;
        private float left;
        private float right;

        private bool constrainHeight;
        private float top;
        private float bottom;

        public patch_RumbleTrigger(EntityData data, Vector2 offset, EntityID id) : base(data, offset, id) {
            // no-op.
        }

        public extern void orig_ctor(EntityData data, Vector2 offset, EntityID id);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset, EntityID id) {
            orig_ctor(data, offset, id);

            constrainHeight = data.Bool("constrainHeight");

            Vector2[] nodes = data.NodesOffset(offset);
            if (nodes.Length >= 2) {
                top = Math.Min(nodes[0].Y, nodes[1].Y);
                bottom = Math.Max(nodes[0].Y, nodes[1].Y);
            }
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchRumbleTriggerAwake] // ... except for manually manipulating the method via MonoModRules
        public extern override void Awake(Scene scene);

        public static void ManuallyTrigger(Vector2 position, float delay, bool triggerUnconstrained = true) {
            foreach (patch_RumbleTrigger rumbleTrigger in Engine.Scene.Entities.FindAll<patch_RumbleTrigger>()) {
                if (rumbleTrigger.manualTrigger && position.X >= rumbleTrigger.left && position.X <= rumbleTrigger.right) {
                    if (rumbleTrigger.constrainHeight) {
                        if (position.Y >= rumbleTrigger.top && position.Y <= rumbleTrigger.bottom)
                            rumbleTrigger.Invoke(delay);
                    } else if (triggerUnconstrained)
                        rumbleTrigger.Invoke(delay);
                }
            }
        }

        [MonoModIgnore]
        public extern void Invoke(float delay);

    }
}

namespace MonoMod {
    /// <summary>
    /// Include the option to use Y range of trigger nodes.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchRumbleTriggerAwake))]
    class PatchRumbleTriggerAwakeAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchRumbleTriggerAwake(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_entity_get_Y = MonoModRule.Modder.FindType("Monocle.Entity").Resolve().FindMethod("get_Y");

            FieldDefinition f_constrainHeight = method.DeclaringType.FindField("constrainHeight");
            FieldDefinition f_top = method.DeclaringType.FindField("top");
            FieldDefinition f_bottom = method.DeclaringType.FindField("bottom");

            bool found = false;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            ILProcessor il = method.Body.GetILProcessor();
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];
                /*
                    ldloc.*
                    callvirt  instance float32 Monocle.Entity::get_X()
                    ldarg.0
                    ldfld     float32 Celeste.RumbleTrigger::left
                    blt.un.s  ldloca.s
                    ldloc.*
                    callvirt  instance float32 Monocle.Entity::get_X()
                    ldarg.0
                    ldfld     float32 Celeste.RumbleTrigger::right // We are here
                    bgt.un.s  ldloca.s
                */
                if (instr.MatchLdfld("Celeste.RumbleTrigger", "right")) {
                    Instruction noYConstraintTarget = instrs[instri - 8];

                    // Copy relevant instructions and modify as needed
                    Instruction[] instrCopy = new Instruction[10];
                    for (int i = 0; i < 10; i++) {
                        instrCopy[i] = il.Create(instrs[instri + i - 8].OpCode, instrs[instri + i - 8].Operand);
                        if (instrCopy[i].OpCode == OpCodes.Callvirt) {
                            instrCopy[i].Operand = m_entity_get_Y;
                        }
                        if (instrCopy[i].MatchLdfld("Celeste.RumbleTrigger", "left")) {
                            instrCopy[i].Operand = f_top;
                        }
                        if (instrCopy[i].MatchLdfld("Celeste.RumbleTrigger", "right")) {
                            instrCopy[i].Operand = f_bottom;
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

                    found = true;
                }
            }

            if (!found) {
                throw new Exception("Instructions to copy were not found in " + method.FullName + "!");
            }
        }

    }
}
