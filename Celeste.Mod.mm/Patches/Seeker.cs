#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null

using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;

namespace Celeste.Patches {
    public class patch_Seeker : Seeker {
        
        // We're effectively in Seeker, but still need to "expose" private fields to our mod.
        private StateMachine State;
        
        // no-op - only here to make
        public patch_Seeker(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
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
    }
}