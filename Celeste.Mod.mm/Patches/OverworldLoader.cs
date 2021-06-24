#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod.Core;
using MonoMod;
using System.Collections;
using System.Threading;

namespace Celeste {
    class patch_OverworldLoader : OverworldLoader {

        public patch_OverworldLoader(Overworld.StartMode startMode, HiresSnow snow = null)
            : base(startMode, snow) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore] // don't change anything in the method...
        [PatchTotalHeartGemChecks] // except for replacing TotalHeartGems with TotalHeartGemsInVanilla through MonoModRules
        private extern void CheckVariantsPostcardAtLaunch();

        [MonoModIgnore] // don't change anything in the method...
        [PatchTotalHeartGemChecksInRoutine] // except for replacing TotalHeartGems with TotalHeartGemsInVanilla through MonoModRules
        private extern IEnumerator Routine(Session session);
    }
}
