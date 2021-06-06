#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Xml;

namespace Celeste {
    class patch_Actor : Actor {

        public patch_Actor(Vector2 position)
            : base(position) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIfFlag("V1:TrySquishWiggle")]
        protected new bool TrySquishWiggle(CollisionData data, int wiggleX = 3, int wiggleY = 3) {
            return TrySquishWiggle(data);
        }

        [MonoModIfFlag("V2:TrySquishWiggle")]
        protected bool TrySquishWiggle(CollisionData data) {
            return TrySquishWiggle(data, 3, 3);
        }

    }
}
