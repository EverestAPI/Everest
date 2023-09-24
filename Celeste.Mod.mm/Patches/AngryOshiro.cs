#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections;

namespace Celeste {
    public class patch_AngryOshiro : AngryOshiro {

        // We're effectively in AngryOshiro, but still need to "expose" private fields to our mod.
        private StateMachine state;

        public patch_AngryOshiro(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }
        
        public extern void orig_ctor(Vector2 position, bool fromCutscene);
        [MonoModConstructor]
        public void ctor(Vector2 position, bool fromCutscene) {
            orig_ctor(position, fromCutscene);

            // setup vanilla state names
            ((patch_StateMachine) state).SetStateName(0, "Chase");
            ((patch_StateMachine) state).SetStateName(1, "ChargeUp");
            ((patch_StateMachine) state).SetStateName(2, "Attack");
            ((patch_StateMachine) state).SetStateName(3, "Dummy");
            ((patch_StateMachine) state).SetStateName(4, "Waiting");
            ((patch_StateMachine) state).SetStateName(5, "Hurt");
            // then allow mods to register new ones
            Everest.Events.AngryOshiro.RegisterStates(this);
        }

        /// <summary>
        /// Adds a new state to this oshiro with the given behaviour, and returns the index of the new state.
        ///
        /// States should always be added during the <c>Events.AngryOshiro.OnRegisterStates</c> event.
        /// </summary>
        /// <param name="name">The name of this state, for display purposes by mods only.</param>
        /// <param name="onUpdate">A function to run every frame during this state, returning the index of the state that should be switched to next frame.</param>
        /// <param name="coroutine">A function that creates a coroutine to run when this state is switched to.</param>
        /// <param name="begin">An action to run when this state is switched to.</param>
        /// <param name="end">An action to run when this state ends.</param>
        /// <returns>The index of the new state.</returns>
        public int AddState(string name, Func<AngryOshiro, int> onUpdate, Func<AngryOshiro, IEnumerator> coroutine = null, Action<AngryOshiro> begin = null, Action<AngryOshiro> end = null){
            return ((patch_StateMachine)state).AddState(name, onUpdate, coroutine, begin, end);
        }
    }
}