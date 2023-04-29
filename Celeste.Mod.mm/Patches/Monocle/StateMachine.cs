#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;
using System;
using System.Collections;

namespace Monocle {
    public class patch_StateMachine : StateMachine {

        // We're effectively in StateMachine, but still need to "expose" private fields to our mod.
        private Action[] begins;
        private Func<int>[] updates;
        private Action[] ends;
        private Func<IEnumerator>[] coroutines;
        
        // Keep track of state's names.
        private string[] names;

        private extern void orig_ctor(int maxStates = 10);
        [MonoModConstructor]
        public void ctor(int maxStates = 10) {
            orig_ctor(maxStates);
            names = new string[maxStates];
        }
        
        private int Expand() {
            int nextIdx = begins.Length;

            int newLength = begins.Length + 1;
            Array.Resize(ref begins, newLength);
            Array.Resize(ref updates, newLength);
            Array.Resize(ref ends, newLength);
            Array.Resize(ref coroutines, newLength);
            Array.Resize(ref names, newLength);

            return nextIdx;
        }
        
        /// <summary>
        /// Adds a new state to this state machine with the given behaviour, and returns the index of the new state.
        /// </summary>
        /// <param name="name">The name of this state, for display purposes by mods only.</param>
        /// <param name="onUpdate">A function to run every frame during this state, returning the index of the state that should be switched to next frame.</param>
        /// <param name="coroutine">A function that creates a coroutine to run when this state is switched to.</param>
        /// <param name="begin">An action to run when this state is switched to.</param>
        /// <param name="end">An action to run when this state ends.</param>
        /// <returns>The index of the new state.</returns>
        public int AddState(string name, Func<int> onUpdate, Func<IEnumerator> coroutine = null, Action begin = null, Action end = null){
            int nextIdx = Expand();
            SetCallbacks(nextIdx, onUpdate, coroutine, begin, end);
            names[nextIdx] = name;
            return nextIdx;
        }
        
        /// <summary>
        /// Adds a new state to this state machine with the given behaviour, providing access to the entity running this state machine.
        ///
        /// It's preferable to use the <c>AddState</c> methods provided in <c>Player</c>, <c>AngryOshiro</c>, and <c>Seeker</c> than to use this directly, as
        /// they ensure the correct type is used. If the entity has the wrong type, then these functions are given <c>null</c>.
        /// </summary>
        /// <param name="name">The name of this state, for display purposes by mods only.</param>
        /// <param name="onUpdate">A function to run every frame during this state, returning the index of the state that should be switched to next frame.</param>
        /// <param name="coroutine">A function that creates a coroutine to run when this state is switched to.</param>
        /// <param name="begin">An action to run when this state is switched to.</param>
        /// <param name="end">An action to run when this state ends.</param>
        /// <typeparam name="E">The type of the entity that these functions run on. If the entity has the wrong type, the functions are given <c>null</c>.</typeparam>
        /// <returns>The index of the new state.</returns>
        public int AddState<E>(string name, Func<E, int> onUpdate, Func<E, IEnumerator> coroutine = null, Action<E> begin = null, Action<E> end = null)
            where E : Entity
        {
            int nextIdx = Expand();
            SetCallbacks(nextIdx, () => onUpdate(Entity as E),
                coroutine == null ? null : () =>  coroutine(Entity as E),
                begin == null ? null : () => begin(Entity as E),
                end == null ? null : () => end(Entity as E));
            names[nextIdx] = name;
            return nextIdx;
        }

        // Mods that expand the state machine manually won't increase the size of `names`, so we need to bounds-check
        // accesses ourselves, both against `begins` ("is it an actual valid state") and `names` ("does it have a coherent name")
        private void CheckBounds(int state) {
            if (!(state < begins.Length && state >= 0))
                throw new IndexOutOfRangeException($"State {state} is out of range, maximum is {begins.Length}.");
        }

        /// <summary>
        /// Returns the name of the state with the given index, which defaults to the index in string form.
        /// These names are for display purposes by mods only.
        /// </summary>
        /// <param name="state">The index of the state.</param>
        /// <returns>The display name of that state.</returns>
        public string GetStateName(int state) {
            CheckBounds(state);
            return (state < names.Length ? names[state] : null) ?? state.ToString();
        }

        /// <summary>
        /// Sets the name of the state with the given index.
        /// These names are for display purposes by mods only.
        /// </summary>
        /// <param name="state">The index of the state.</param>
        /// <param name="name">The new display name it should use.</param>
        public void SetStateName(int state, string name) {
            CheckBounds(state);
            if (state >= names.Length)
                Array.Resize(ref names, state + 1);
            names[state] = name;
        }

        /// <summary>
        /// Returns the name of the current state, which defaults to the index in string form.
        /// These names are for display purposes by mods only.
        /// </summary>
        /// <returns>The display name of the current state.</returns>
        public string GetCurrentStateName() => GetStateName(State);
    }
}