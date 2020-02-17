using MonoMod;

namespace Celeste {
    class patch_OuiJournalGlobal : OuiJournalGlobal {
        public patch_OuiJournalGlobal(OuiJournal journal)
            : base(journal) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModConstructor]
        [MonoModIgnore] // don't change anything in the method...
        [PatchOuiJournalStatsHeartGemCheck] // except for replacing TotalHeartGems with TotalHeartGemsInVanilla through MonoModRules
        public extern void ctor(OuiJournal journal);

    }
}