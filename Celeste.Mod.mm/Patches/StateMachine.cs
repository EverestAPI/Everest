using Monocle;
using MonoMod;
using System;
using System.Collections;

namespace Celeste {
    public class patch_StateMachine : StateMachine {

        // We're effectively in StateMachine, but still need to "expose" private fields to our mod.
        private Action[] begins;
        private Func<int>[] updates;
        private Action[] ends;
        private Func<IEnumerator>[] coroutines;
        
        // Keep track of state's names.
        private string[] names;

        public extern void orig_ctor(int maxStates = 10);
        [MonoModConstructor]
        public void ctor(int maxStates = 10) {
            orig_ctor(maxStates);
            names = new string[maxStates];
        }
        
        private int Expand() {
            int nextIdx = begins.Length;
            
            Array.Resize(ref begins, begins.Length + 1);
            Array.Resize(ref updates, updates.Length + 1);
            Array.Resize(ref ends, ends.Length + 1);
            Array.Resize(ref coroutines, coroutines.Length + 1);
            Array.Resize(ref names, names.Length + 1);

            return nextIdx;
        }

        public extern void orig_SetCallbacks(int state, Func<int> onUpdate, Func<IEnumerator> coroutine = null, Action begin = null, Action end = null);
        public void SetCallbacks(
            int state,
            Func<int> onUpdate,
            Func<IEnumerator> coroutine = null,
            Action begin = null,
            Action end = null) {
            orig_SetCallbacks(state, onUpdate, coroutine, begin, end);
            names[state] = state.ToString();
        }
        
        /// <summary>
        /// Adds a state to this StateMachine.
        /// </summary>
        /// <returns>The index of the new state.</returns>
        public int AddState(string name, Func<Entity, int> onUpdate, Func<Entity, IEnumerator> coroutine = null, Action<Entity> begin = null, Action<Entity> end = null){
            int nextIdx = Expand();
            SetCallbacks(nextIdx, Helper.WrapFunc(onUpdate, this),
                Helper.WrapFunc(coroutine, this),
                Helper.WrapAction(begin, this),
                Helper.WrapAction(end, this));
            names[nextIdx] = name;
            return nextIdx;
        }

        public string GetState(int state) => names[state];
        public void SetStateName(int state, string name) => names[state] = name;
        
        public string GetCurrentStateName() => names[State];
    }

    internal static class Helper {
        internal static Func<T> WrapFunc<T>(Func<Entity, T> f, Component c) => f == null ? null : () => f(c.Entity);
        internal static Action WrapAction(Action<Entity> a, Component c) => a == null ? null : () => a(c.Entity);
    }
}