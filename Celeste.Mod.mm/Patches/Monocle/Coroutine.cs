#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monocle {
    class patch_Coroutine : Coroutine {

        // We're effectively in Coroutine, but still need to "expose" private fields to our mod.
        private float waitTimer;

        // Forcibly set the timer to 0 to jump to the next "step."
        public void Jump() {
            waitTimer = 0;
        }

    }
    public static class CoroutineExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static void Jump(this Coroutine self)
            => ((patch_Coroutine) self).Jump();

    }
}
