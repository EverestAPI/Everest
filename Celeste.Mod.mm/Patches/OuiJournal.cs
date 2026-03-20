#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using System;
using System.Collections;

using _OuiJournal = Celeste.OuiJournal;

namespace Celeste {
    class patch_OuiJournal : OuiJournal {

        public extern IEnumerator orig_Enter(Oui from);
        public override IEnumerator Enter(Oui from) {
            IEnumerator orig = orig_Enter(from);

            // Populate page list
            // The page list is always populated before the first yield statement
            bool done = !orig.MoveNext();
            object first = done ? null : orig.Current;

            Everest.Events.Journal.Enter(this, from);

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

namespace Celeste.Mod {
    public static partial class Everest {
        public static partial class Events {
            [Obsolete("Use Journal instead.")]
            public static class OuiJournal {
                public delegate void EnterHandler(_OuiJournal journal, Oui from);
                public static event EnterHandler OnCreateButtons {
                    add {
                        Journal.OnEnter += (Journal.EnterHandler) value.CastDelegate(typeof(Journal.EnterHandler));
                    }
                    remove {
                        Journal.OnEnter -= (Journal.EnterHandler) value.CastDelegate(typeof(Journal.EnterHandler));
                    }
                }
            }

            public static class Journal {
                public delegate void EnterHandler(_OuiJournal journal, Oui from);
                /// <summary>
                /// Called by <see cref="patch_OuiJournal.Enter"/>.
                /// </summary>
                public static event EnterHandler OnEnter;
                internal static void Enter(_OuiJournal journal, Oui from)
                    => OnEnter?.Invoke(journal, from);
            }

        }
    }
}
