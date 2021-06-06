using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_RainFG : RainFG {

        public new Color? Color;

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchRainFGRender] // ... except for manually manipulating the method via MonoModRules
        public new extern void Render(Scene scene);

        private Color _GetColor(string orig) {
            return Color ?? Calc.HexToColor(orig);
        }

    }
}
