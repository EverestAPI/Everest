#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;

namespace Monocle {
    // No public constructors.
    class patch_EntityList {

        // We're effectively in EntityList, but still need to "expose" private fields to our mod.
        private List<Entity> toAdd;
        private List<Entity> entities;

        /// <summary>
        /// The list of entities which are about to get added.
        /// </summary>
        public List<Entity> ToAdd => toAdd;

        internal void ClearEntities() {
            entities.Clear();
        }

        [MonoModIgnore]
        [PatchEntityListUpdate]
        internal extern void Update();
    }
    public static class EntityListExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Get the list of entities which are about to get added.
        /// </summary>
        public static List<Entity> GetToAdd(this EntityList self)
            => ((patch_EntityList) (object) self).ToAdd;

    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the method to run UpdatePreceder and UpdateFinalizer
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchEntityListUpdate))]
    class PatchEntityListUpdateAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchEntityListUpdate(ILContext context, CustomAttribute attrib) {
            TypeDefinition Entity = MonoModRule.Modder.FindType("Monocle.Entity").Resolve();
            MethodDefinition entity_UpdatePreceder = Entity.FindMethod("PreUpdate");
            MethodDefinition entity_UpdateFinalizer = Entity.FindMethod("PostUpdate");

            ILCursor cursor = new ILCursor(context);
            ILLabel branch = null;
            cursor.GotoNext(MoveType.After, instr => instr.MatchBr(out branch));
            cursor.GotoNext(MoveType.After, instr => instr.MatchStloc(1));
            cursor.Emit(OpCodes.Ldloc_1);
            cursor.Emit(OpCodes.Callvirt, entity_UpdatePreceder);
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdloca(0), i2 => i2.MatchCall(out MethodReference method) && method.Name == "MoveNext");
            cursor.Emit(OpCodes.Ldloc_1);
            cursor.Emit(OpCodes.Callvirt, entity_UpdateFinalizer);
            cursor.MarkLabel(branch);
        }

    }
}
