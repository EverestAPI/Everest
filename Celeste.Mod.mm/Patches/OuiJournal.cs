#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using System;
using System.Collections;

namespace Celeste {
	class patch_OuiJournal : OuiJournal {
		public extern IEnumerator orig_Enter(Oui from);
		public override IEnumerator Enter(Oui from) {
			var enumerator = orig_Enter(from);

			// Populate page list
			// The page list is always populated before the first yield statement
			bool done = !enumerator.MoveNext();
			Object first = done ? null : enumerator.Current;			

			Everest.Events.OuiJournal.Enter(from, this);

			// Recalculate page numbers
			int pageNum = 0;
			foreach (OuiJournalPage page in Pages)
				page.PageIndex = pageNum++;

			// Iterate over the rest of the enumerator
			if (!done)
			{
				yield return first;
				while (enumerator.MoveNext())
					yield return enumerator.Current;
			}
		}
	}
}