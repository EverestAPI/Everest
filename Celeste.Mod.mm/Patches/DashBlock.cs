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
    class patch_DashBlock : DashBlock {

        public patch_DashBlock(Vector2 position, char tiletype, float width, float height, bool blendIn, bool permanent, bool canDash, EntityID id)
            : base(position, tiletype, width, height, blendIn, permanent, canDash, id) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public void Break(Vector2 from, Vector2 direction, bool playSound = true) {
            Break(from, direction, playSound, true);
        }

    }
}
