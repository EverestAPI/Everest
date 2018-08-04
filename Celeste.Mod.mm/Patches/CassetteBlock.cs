#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;
using System.IO;
using FMOD.Studio;
using Monocle;
using Celeste.Mod.Meta;

namespace Celeste {
    class patch_CassetteBlock : CassetteBlock {

        private Color color;

        public patch_CassetteBlock(Vector2 position, float width, float height, int index)
            : base(position, width, height, index) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(Vector2 position, float width, float height, int index);
        [MonoModConstructor]
        public void ctor(Vector2 position, float width, float height, int index) {
            orig_ctor(position, width, height, index);

            // Original index == 2 color is too similar to index == 1
            if (index == 2)
                color = Calc.HexToColor("47cb83");
        }

    }
}
