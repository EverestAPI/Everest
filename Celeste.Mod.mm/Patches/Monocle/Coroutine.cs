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

        /// <summary>
        /// Force this coroutine to mimic vanilla behavior:<br></br>
        /// When yield returning a new IEnumerator or when a previously IEnumerator finishes,
        /// the next IEnumerator runs delayed by one frame.<br></br>
        /// <br></br>
        /// To describe the behavior of this field, imagine the following code replacing all yield returns of IEnumerators:<br></br>
        /// <code>
        /// IEnumerator next = Nested(...);<br></br>
        /// if (ForceDelayedSwap ?? vanilla) yield return null;<br></br>
        /// while (next.MoveNext()) yield return next.Current;<br></br>
        /// if (ForceDelayedSwap ?? vanilla) yield return null;<br></br>
        /// // Control is returned to your code here.<br></br>
        /// </code>
        /// </summary>
        public bool? ForceDelayedSwap;

        /// <summary>
        /// Forcibly set the timer to 0 to jump to the next "step."
        /// </summary>
        public void Jump() {
            waitTimer = 0;
        }

        public extern void orig_Update();
        public override void Update() {
            IEnumerator prev, next;
            do {
                prev = enumerators.Count > 0 ? enumerators.Peek() : null;
                orig_Update();
                next = enumerators.Count > 0 ? enumerators.Peek() : null;

                if (prev == next && next != null && next.Current is Action<patch_Coroutine> cb) {
                    cb(this);
                    prev = null;
                }

            } while (prev != next && (prev == null || (next != null && !(ForceDelayedSwap ?? isVanillaSwitch(prev, next)))));
        }

        [PatchCoroutineHackfixHelperStateMachine] // references to CoroutineDelayHackfixHelper actually refer to CoroutineDelayHackfixHelper.<Wrap>d__2 or whatever the compiler generates
        private static bool isVanillaSwitch(IEnumerator prev, IEnumerator next) {
            // we are switching between two vanilla coroutines if:
            // - they are both from Celeste.exe
            // - they aren't from CoroutineDelayHackfixHelper.Wrap, because that would mean they are actually a wrapped hook from a mod
            return prev.GetType().Assembly == typeof(Engine).Assembly && prev.GetType() != typeof(CoroutineDelayHackfixHelper)
                && next.GetType().Assembly == typeof(Engine).Assembly && next.GetType() != typeof(CoroutineDelayHackfixHelper);
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
