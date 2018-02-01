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

        // Mods can't access patch_Coroutine directly.
        // We thus expose it through an extension.
        public static void Jump(this Coroutine c) {
            // patch_Coroutine becomes Coroutine at patch time.
            ((patch_Coroutine) c).Jump();
        }

    }
}
