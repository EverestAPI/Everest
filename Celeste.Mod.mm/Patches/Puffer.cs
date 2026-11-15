using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;

namespace Celeste {
    class patch_Puffer : Puffer {
        public patch_Puffer(EntityData data, Vector2 offset) : base(data, offset) {
            // ignored by MonoMod
        }

        [MonoModLinkTo("Monocle.Entity", "Added")]
        [MonoModIgnore]
        public extern void base_Added(Scene scene);

        public override void Added(Scene scene) {
            base_Added(scene);
            if (Depth == 0 && ((patch_AreaKey) (object) (scene as Level).Session.Area).LevelSet != "Celeste") {
                Depth = -1; // makes puffer boosts work after player respawn or other depth reset
            }
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchPufferExplode] // ... except for manually manipulating the method via MonoModRules
        public extern void Explode();
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch puffer explosions to handle explosion interactions using <see cref="ExplosionCollider"/> components, rather than hardcoding them.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchPufferExplode))]
    class PatchPufferExplodeAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchPufferExplode(ILContext context, CustomAttribute attrib) {
            TypeDefinition t_ExplosionCollider = MonoModRule.Modder.Module.GetType("Celeste.Mod.Entities.ExplosionCollider");
            MethodDefinition m_ExplosionCollider_TriggerExplosionColliders = t_ExplosionCollider.FindMethod("System.Void TriggerExplosionColliders(Monocle.Entity)");

            ILCursor cursor = new(context);

            // remove hardcoded explosion interactions (excluding for players and theo crystals)

            // jump to just before the first hardcoded interaction (TempleCrackedBlocks)
            cursor.GotoNext(MoveType.Before,
                instr => instr.MatchLdarg0(),
                instr => instr.MatchCall("Monocle.Entity", "get_Scene"),
                instr => instr.MatchCallvirt("Monocle.Scene", "get_Tracker"),
                instr => instr.MatchCallvirt(out MethodReference m)
                         && m.FullName == "System.Collections.Generic.List`1<Monocle.Entity> Monocle.Tracker::GetEntities<Celeste.TempleCrackedBlock>()");

            // get the (inclusive) start index of the code to remove
            int removeStartIndex = cursor.Index;

            // jump to just before the collider is reset to normal (just after the last hardcoded interaction)
            cursor.GotoNext(MoveType.Before,
                instr => instr.MatchLdarg0(),
                instr => instr.MatchLdloc0(),
                instr => instr.MatchCall("Monocle.Entity", "set_Collider"));

            // get the (exclusive) end index of the code to remove
            int removeEndIndex = cursor.Index;

            // remove the try/finally blocks used by the foreach loops over entities with hardcoded explosion interactions
            for (int i = context.Body.ExceptionHandlers.Count - 1; i >= 0; i--) {
                int tryStartIndex = context.IndexOf(context.Body.ExceptionHandlers[i].TryStart);
                if (tryStartIndex >= removeStartIndex && tryStartIndex < removeEndIndex) {
                    context.Body.ExceptionHandlers.RemoveAt(i);
                }
            }

            // remove hardcoded explosion interactions
            cursor.Index = removeStartIndex;
            cursor.RemoveRange(removeEndIndex - removeStartIndex);
            cursor.MoveAfterLabels();

            // remove locals used by the foreach loops over entities with hardcoded explosion interactions
            for (int i = context.Body.Variables.Count - 1; i >= 0; i--) {
                switch (context.Body.Variables[i].VariableType.FullName) {
                    case "System.Collections.Generic.List`1/Enumerator<Monocle.Entity>":
                    case "Celeste.TempleCrackedBlock":
                    case "Celeste.TouchSwitch":
                    case "Celeste.FloatingDebris":
                        context.Body.Variables.RemoveAt(i);
                        break;
                }
            }

            // inject call to ExplosionCollider.TriggerExplosionColliders
            cursor.EmitLdarg0();
            cursor.EmitCall(m_ExplosionCollider_TriggerExplosionColliders);
        }
    }
}
