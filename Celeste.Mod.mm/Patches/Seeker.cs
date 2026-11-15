#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null

using Celeste.Mod;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using MonoMod.Utils;
using System;
using System.Collections;

using _Seeker = Celeste.Seeker;

namespace Celeste {
    public class patch_Seeker : Seeker {

        // We're effectively in Seeker, but still need to "expose" private fields to our mod.
        private StateMachine State;

        // no-op - only here to make
        public patch_Seeker(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(Vector2 position, Vector2[] patrolPoints);
        [MonoModConstructor]
        public void ctor(Vector2 position, Vector2[] patrolPoints) {
            orig_ctor(position, patrolPoints);

            // setup vanilla state names
            ((patch_StateMachine) State).SetStateName(0, "Idle");
            ((patch_StateMachine) State).SetStateName(1, "Patrol");
            ((patch_StateMachine) State).SetStateName(2, "Spotted");
            ((patch_StateMachine) State).SetStateName(3, "Attack");
            ((patch_StateMachine) State).SetStateName(4, "Stunned");
            ((patch_StateMachine) State).SetStateName(5, "Skidding");
            ((patch_StateMachine) State).SetStateName(6, "Regenerate");
            ((patch_StateMachine) State).SetStateName(7, "Returned");
            // then allow mods to register new ones
            Everest.Events.Seeker.RegisterStates(this);
        }

        /// <summary>
        /// Adds a new state to this seeker with the given behaviour, and returns the index of the new state.
        ///
        /// States should always be added at the end of the <c>Seeker(Vector2, Vector2[])</c> constructor.
        /// </summary>
        /// <param name="name">The name of this state, for display purposes by mods only.</param>
        /// <param name="onUpdate">A function to run every frame during this state, returning the index of the state that should be switched to next frame.</param>
        /// <param name="coroutine">A function that creates a coroutine to run when this state is switched to.</param>
        /// <param name="begin">An action to run when this state is switched to.</param>
        /// <param name="end">An action to run when this state ends.</param>
        /// <returns>The index of the new state.</returns>
        public int AddState(string name, Func<Seeker, int> onUpdate, Func<Seeker, IEnumerator> coroutine = null, Action<Seeker> begin = null, Action<Seeker> end = null){
            return ((patch_StateMachine)State).AddState(name, onUpdate, coroutine, begin, end);
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchSeekerRegenerateCoroutine] // ... except for manually manipulating the method via MonoModRules
        private extern IEnumerator RegenerateCoroutine();
    }
}

namespace Celeste.Mod {
    public static partial class Everest {
        public static partial class Events {
            public static class Seeker {
                /// <summary>
                /// Called in the Seeker constructor during <see cref="StateMachine"/> initialisation, to be used to register custom Seeker states.
                /// </summary>
                public static event Action<_Seeker> OnRegisterStates;
                internal static void RegisterStates(_Seeker seeker)
                    => OnRegisterStates?.Invoke(seeker);
            }
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch seeker regenerate explosions to handle explosion interactions using <see cref="ExplosionCollider"/> components, rather than hardcoding them.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchSeekerRegenerateCoroutine))]
    class PatchSeekerRegenerateCoroutineAttribute : Attribute { }

    static partial class MonoModRules {
        public static void PatchSeekerRegenerateCoroutine(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition routine = method.GetEnumeratorMoveNext();

            new ILContext(routine).Invoke(ctx => {
                TypeDefinition t_ExplosionCollider = MonoModRule.Modder.Module.GetType("Celeste.Mod.Entities.ExplosionCollider");
                MethodDefinition m_ExplosionCollider_TriggerExplosionColliders = t_ExplosionCollider.FindMethod("System.Void TriggerExplosionColliders(Monocle.Entity)");

                ILCursor cursor = new(ctx);

                // remove hardcoded explosion interactions (excluding players and theo crystals)

                // jump to just before the first hardcoded interaction (TempleCrackedBlocks)
                cursor.GotoNext(MoveType.Before,
                    instr => instr.MatchLdloc1(),
                    instr => instr.MatchCallvirt("Monocle.Entity", "get_Scene"),
                    instr => instr.MatchCallvirt("Monocle.Scene", "get_Tracker"),
                    instr => instr.MatchCallvirt(out MethodReference m)
                             && m.FullName == "System.Collections.Generic.List`1<Monocle.Entity> Monocle.Tracker::GetEntities<Celeste.TempleCrackedBlock>()");

                // get the (inclusive) start index of the code to remove
                int removeStartIndex = cursor.Index;

                // jump to just before the collider is reset to normal (just after the last hardcoded interaction)
                cursor.GotoNext(MoveType.Before,
                    instr => instr.MatchLdloc1(),
                    instr => instr.MatchLdloc1(),
                    instr => instr.MatchLdfld("Celeste.Seeker", "physicsHitbox"),
                    instr => instr.MatchCallvirt("Monocle.Entity", "set_Collider"));

                // get the (exclusive) end index of the code to remove
                int removeEndIndex = cursor.Index;

                // remove the try/finally blocks used by the foreach loops over entities with hardcoded explosion interactions
                for (int i = ctx.Body.ExceptionHandlers.Count - 1; i >= 0; i--) {
                    int tryStartIndex = ctx.IndexOf(ctx.Body.ExceptionHandlers[i].TryStart);
                    if (tryStartIndex >= removeStartIndex && tryStartIndex < removeEndIndex) {
                        ctx.Body.ExceptionHandlers.RemoveAt(i);
                    }
                }

                // remove hardcoded explosion interactions
                cursor.Index = removeStartIndex;
                cursor.RemoveRange(removeEndIndex - removeStartIndex);
                cursor.MoveAfterLabels();

                // remove locals used by the foreach loops over entities with hardcoded explosion interactions
                for (int i = ctx.Body.Variables.Count - 1; i >= 0; i--) {
                    switch (ctx.Body.Variables[i].VariableType.FullName) {
                        case "System.Collections.Generic.List`1/Enumerator<Monocle.Entity>":
                        case "Celeste.TempleCrackedBlock":
                        case "Celeste.TouchSwitch":
                            ctx.Body.Variables.RemoveAt(i);
                            break;
                    }
                }

                // inject call to ExplosionCollider.TriggerExplosionColliders
                cursor.EmitLdloc1();
                cursor.EmitCall(m_ExplosionCollider_TriggerExplosionColliders);
            });
        }
    }
}
