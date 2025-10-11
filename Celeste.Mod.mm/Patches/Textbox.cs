#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using System;
using System.Collections;
using Celeste.Mod;
using MonoMod;

namespace Celeste {
    class patch_Textbox : Textbox {

        // We're effectively in Textbox, but still need to "expose" private fields to our mod.
        private Func<IEnumerator>[] events;

        public patch_Textbox(string dialog, Language language, params Func<IEnumerator>[] events)
            : base(dialog, language, events) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(string dialog, Language language, params Func<IEnumerator>[] events);
        [MonoModConstructor]
        public void ctor(string dialog, Language language, params Func<IEnumerator>[] events) {
            orig_ctor(dialog, language, events);

            var extraEvents = Everest.Events.Textbox.AddCustomEvents(this, dialog, language);
            if (extraEvents.Count > 0) {
                int initialCount = events.Length;
                Array.Resize(ref events, initialCount + extraEvents.Count);

                for (int i = 0; i < extraEvents.Count; i++)
                    events[initialCount + i] = extraEvents[i];
            }
        }

    }
}
