#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste;
using Celeste.Mod.Registry;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

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

        [MonoModIgnore]
        [PatchEntityListUpdateLists]
        public extern void UpdateLists();

        [MonoModIgnore]
        [PatchEntityListAdd]
        public extern void Add(Entity entity);

        private void _HandleEntityData(Entity entity) {
            if (patch_Level._currentEntityData is { } data && ((patch_Entity)entity).SourceData is null) {
                ((patch_Entity)entity).SourceData = data;
                ((patch_Entity)entity).SourceId = patch_Level._currentEntityId;
                // We don't reset '_currentEntityData' here, in case an entity Adds entities in the ctor,
                // in which case we'll see that other entity before the actual entity,
                // meaning the real entity wouldn't get its SourceData set at all,
                // which is worse than setting it for an unrelated entity.
                // This can only happen with entities added via the legacy Everest.Events.Level.LoadEntity event anyway,
                // so this should be very rare in practice.
            }
        }
    }

    public static class EntityListExt {

        /// <summary>
        /// Get the list of entities which are about to get added.
        /// </summary>
        [Obsolete("Use EntityList.ToAdd instead.")]
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

    /// <summary>
    /// Improves performance by removing redundant Contains()
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchEntityListUpdateLists))]
    class PatchEntityListUpdateListsAttribute : Attribute { }

    /// <summary>
    /// Make Add(entity) method to crash when entity is null
    /// so modders can catch bugs in time
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchEntityListAdd))]
    class PatchEntityListAddAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchEntityListUpdate(ILContext context, CustomAttribute attrib) {
            TypeDefinition Entity = MonoModRule.Modder.FindType("Monocle.Entity").Resolve();
            MethodDefinition entity_UpdatePreceder = Entity.FindMethod("_PreUpdate");
            MethodDefinition entity_UpdateFinalizer = Entity.FindMethod("_PostUpdate");

            ILCursor cursor = new ILCursor(context);
            ILLabel branchMatch = null;
            cursor.GotoNext(MoveType.After, instr => instr.MatchBr(out branchMatch));
            cursor.GotoNext(MoveType.After, instr => instr.MatchStloc(1));
            cursor.Emit(OpCodes.Ldloc_1);
            cursor.Emit(OpCodes.Callvirt, entity_UpdatePreceder);
            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchLdloca(0), i2 => i2.MatchCall(out MethodReference method) && method.Name == "MoveNext");
            cursor.Emit(OpCodes.Ldloc_1);
            cursor.Emit(OpCodes.Callvirt, entity_UpdateFinalizer);
            cursor.MarkLabel(branchMatch);
        }

        public static void PatchEntityListUpdateLists(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new ILCursor(context);

            // if (!current.Contains(entity)) {          if (current.Add(entity)) {
            //     current.Add(entity);           ->         entities.Add(entity);
            //     entities.Add(entity);                 }
            // }
            cursor.GotoNext(
                instr => instr.MatchLdarg(0),
                instr => instr.MatchLdfld(out _),
                instr => instr.MatchLdloc(1),
                instr => instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference).GetID().Contains("HashSet`1<Monocle.Entity>::Add"));
            object hashAddOperand = cursor.Instrs[cursor.Index + 2].Next.Operand;
            cursor.RemoveRange(5);
            cursor.GotoPrev(instr => instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference).GetID().Contains("HashSet`1<Monocle.Entity>::Contains"));
            cursor.Next.Operand = hashAddOperand;
            cursor.Next.Next.OpCode = OpCodes.Brfalse_S;

            // if (entities.Contains(entity)) {             if (current.Remove(entity)) {
            //     current.Remove(entity);           ->         entities.Remove(entity);
            //     entities.Remove(entity);                 }
            // }
            cursor.GotoNext(
                instr => instr.MatchLdarg(0),
                instr => instr.MatchLdfld(out _),
                instr => instr.MatchLdloc(3),
                instr => instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference).GetID().Contains("HashSet`1<Monocle.Entity>::Remove"));
            object currentOperand = cursor.Next.Next.Operand;
            object hashRemoveOperand = cursor.Instrs[cursor.Index + 2].Next.Operand;
            cursor.RemoveRange(5);
            cursor.GotoPrev(instr => instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference).GetID().Contains("List`1<Monocle.Entity>::Contains"));
            cursor.Prev.Previous.Operand = currentOperand;
            cursor.Next.Operand = hashRemoveOperand;
        }

        public static void PatchEntityListAdd(ILContext context, CustomAttribute attrib) {
            // insert the following code at the beginning of the method
            // if (entity == null) throw new ArgumentNullException("entity")

            TypeDefinition t_ArgumentNullException = MonoModRule.Modder.FindType("System.ArgumentNullException").Resolve();
            MethodReference ctor_ArgumentNullException = MonoModRule.Modder.Module.ImportReference(t_ArgumentNullException.FindMethod("System.Void .ctor(System.String)"));

            ILCursor cursor = new ILCursor(context);
            ILLabel label = cursor.DefineLabel();
            cursor.Emit(OpCodes.Ldarg_1)
                .Emit(OpCodes.Brtrue_S, label)
                .Emit(OpCodes.Ldstr, "entity")
                .Emit(OpCodes.Newobj, ctor_ArgumentNullException)
                .Emit(OpCodes.Throw)
                .MarkLabel(label);
            
            // Add call to _HandleEntityData to track EntityData -> Entity relations
            TypeDefinition t_EntityList = MonoModRule.Modder.FindType("Monocle.EntityList").Resolve();
            MethodDefinition m_EntityList_HandleEntityData = t_EntityList.FindMethod("_HandleEntityData");
            cursor.GotoNext(MoveType.Before, instr => instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference).GetID().Contains("HashSet`1<Monocle.Entity>::Add"));
            cursor.EmitLdarg0();
            cursor.EmitLdarg1();
            cursor.EmitCall(m_EntityList_HandleEntityData);
        }

    }
}
