#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_RainFG : RainFG {

        public new Color? Color;

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchRainFGRender] // ... except for manually manipulating the method via MonoModRules
        public new extern void Render(Scene scene);

        private static Color _GetColor(string orig, patch_RainFG self) {
            return self.Color ?? Calc.HexToColor(orig);
        }

    }
}
