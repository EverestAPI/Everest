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
            int prevCount, nextCount;
            IEnumerator prev, next;
            do {
                prevCount = enumerators.Count;
                prev = Current;
                orig_Update();
                nextCount = enumerators.Count;
                next = Current;

                if (prev != null) {
                    object current = prev.Current;

                    if (current is Action<patch_Coroutine> cb) {
                        cb(this);
                        continue;

                    } else if (current is SwapImmediately swap) {
                        enumerators.Pop();
                        enumerators.Push(swap.Inner);
                        continue;
                    }
                }

                break;
            } while (true);
        }

    }
    public static class CoroutineExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <inheritdoc cref="patch_Coroutine.Jump"/>
        public static void Jump(this Coroutine self)
            => ((patch_Coroutine) self).Jump();

    }
}
