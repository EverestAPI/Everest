#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0414 // The field is assigned but its value is never used


namespace Monocle {
    class patch_Coroutine : Coroutine {

        // We're effectively in Coroutine, but still need to "expose" private fields to our mod.
        private float waitTimer;

        public void Jump() {
            waitTimer = 0;
        }

    }
    public static class CoroutineExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Forcibly set the timer to 0 to jump to the next "step."
        /// </summary>
        public static void Jump(this Coroutine self)
            => ((patch_Coroutine) self).Jump();

    }
}
