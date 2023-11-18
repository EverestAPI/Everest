#pragma warning disable CS0414 // The field is assigned but its value is never used
#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Monocle {
    class patch_Coroutine : Coroutine {

        // We're effectively in Coroutine, but still need to "expose" private fields to our mod.
        private float waitTimer;
        private Stack<IEnumerator> enumerators;

        public IEnumerator Current => enumerators.Count > 0 ? enumerators.Peek() : null;

        /// <summary>
        /// Forcibly set the timer to 0 to jump to the next "step."
        /// </summary>
        public void Jump() {
            waitTimer = 0;
        }

        public extern void orig_Update();
        public override void Update() {
            do {
                orig_Update();

                // if the coroutine last returned an Action<Coroutine>, run it passing the coroutine.
                if (Current?.Current is Action<patch_Coroutine> cb) {
                    cb(this);
                    continue;
                }

                // if the top of the stack is a SwapImmediately... swap it immediately.
                if (Current is SwapImmediately swap) {
                    enumerators.Pop();
                    enumerators.Push(swap.Inner);
                    continue;
                }

                // if the coroutine last returned a SwapImmediately, this means we returned from a coroutine that swaps immediately...
                // so, swap immediately.
                if (Current?.Current is SwapImmediately) {
                    continue;
                }

                break;
            } while (true);
        }

    }
    public static class CoroutineExt {

        /// <inheritdoc cref="patch_Coroutine.Jump"/>
        [Obsolete("Use Coroutine.Jump instead.")]
        public static void Jump(this Coroutine self)
            => ((patch_Coroutine) self).Jump();

    }
}
