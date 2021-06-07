#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Monocle;
using MonoMod;
using System.Collections;

namespace Celeste {
    class patch_AscendManager {
        [MonoModIgnore]
        [PatchAscendManagerRoutine]
        private extern IEnumerator Routine();

        private bool ShouldRestorePlayerX() {
            return (Engine.Scene as Level).Session.Area.GetLevelSet() != "Celeste";
        }
    }
}