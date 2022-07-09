#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Helpers;
using MonoMod;
using System.Collections;

namespace Celeste {
    class patch_OuiOptions : OuiOptions {
        public extern IEnumerator orig_Leave(Oui next);
        public override IEnumerator Leave(Oui next) {
            if (UserIO.Open(UserIO.Mode.Write)) {
                // VanillaMouseBindings extracts the data from the Settings class
                byte[] data = UserIO.Serialize(new VanillaMouseBindings().Init());
                UserIO.Save<VanillaMouseBindings>("modsettings-Everest_MouseBindings", data);
                UserIO.Close();
            }
            return orig_Leave(next);
        }
    }
}
