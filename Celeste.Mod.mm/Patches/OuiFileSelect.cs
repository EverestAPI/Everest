using MonoMod;
using System.Collections;

namespace Celeste {
    class patch_OuiFileSelect : OuiFileSelect {
        [MonoModIgnore] // we don't want to change anything in the method...
        [PatchOuiFileSelectSubmenuChecks] // ... except manipulating it manually with MonoModRules
        public extern new IEnumerator Enter(Oui from);

        [MonoModIgnore] // we don't want to change anything in the method...
        [PatchOuiFileSelectSubmenuChecks] // ... except manipulating it manually with MonoModRules
        public extern new IEnumerator Leave(Oui next);
    }
}
