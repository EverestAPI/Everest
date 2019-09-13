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
    class patch_DreamBlock : DreamBlock {

        public patch_DreamBlock(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModLinkTo("Celeste.DreamBlock", "System.Void .ctor(Microsoft.Xna.Framework.Vector2,System.Single,System.Single,System.Nullable`1<Microsoft.Xna.Framework.Vector2>,System.Boolean,System.Boolean,System.Boolean)")]
        [MonoModForceCall]
        [MonoModRemove]
        public extern void ctor(Vector2 position, float width, float height, Vector2? node, bool fastMoving, bool oneUse, bool below);
        [MonoModConstructor]
        public void ctor(Vector2 position, float width, float height, Vector2? node, bool fastMoving, bool oneUse) {
            ctor(position, width, height, node, fastMoving, oneUse, false);
        }

    }
}
