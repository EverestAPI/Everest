#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null

using MonoMod;
using System.Collections.Generic;

namespace Monocle {
    class patch_Chooser<T> {

        private List<patch_Choice> choices;

        public List<patch_Choice> Choices => choices;

        [MonoModPublic]
        public class patch_Choice {

        }
    }
}
