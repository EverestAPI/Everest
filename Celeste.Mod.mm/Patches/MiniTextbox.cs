using System.Collections;
using MonoMod;

namespace Celeste {
    class patch_MiniTextbox : MiniTextbox {

        public patch_MiniTextbox(string dialogId)
            : base(dialogId) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchMiniTextboxRoutine]
        private extern IEnumerator Routine();

    }
}
