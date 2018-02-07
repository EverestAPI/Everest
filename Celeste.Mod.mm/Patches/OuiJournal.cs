#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using System;
using System.Collections;

namespace Celeste {
    class patch_OuiJournal : OuiJournal {

        public extern IEnumerator orig_Enter(Oui from);
        public override IEnumerator Enter(Oui from) {
            IEnumerator orig = orig_Enter(from);

            // Populate page list
            // The page list is always populated before the first yield statement
            bool done = !orig.MoveNext();
            object first = done ? null : orig.Current;

            Everest.Events.OuiJournal.Enter(this, from);

            // Recalculate page numbers
            int pageNum = 0;
            foreach (OuiJournalPage page in Pages)
                page.PageIndex = pageNum++;

            // Iterate over the rest of the enumerator
            if (!done) {
                yield return first;
                while (orig.MoveNext())
                    yield return orig.Current;
            }
        }

    }
}