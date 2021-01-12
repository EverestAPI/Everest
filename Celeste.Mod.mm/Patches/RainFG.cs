#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;

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
