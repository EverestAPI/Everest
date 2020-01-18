#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System.Collections;

namespace Celeste {
    class patch_CS10_Ending : CS10_Ending {
        public patch_CS10_Ending(Player player)
            : base(player) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern IEnumerator orig_EndingRoutine();

        private IEnumerator EndingRoutine() {
            patch_AreaComplete.InitAreaCompleteInfoForEverest();

            // call the original EndingRoutine, that will handle displaying the end screen.
            IEnumerator orig = orig_EndingRoutine();
            while (orig.MoveNext())
                yield return orig.Current;

            patch_AreaComplete.DisposeAreaCompleteInfoForEverest();
            yield break;
        }
    }
}
