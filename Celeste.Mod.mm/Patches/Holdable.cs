#pragma warning disable CS0649 // The field is never assigned to, and will always have its default value null

using Microsoft.Xna.Framework;
using MonoMod;
using System;

namespace Celeste {
    class patch_Holdable : Holdable {
        public Action<Vector2> SpeedSetter;

        [MonoModLinkTo("Celeste.Holdable", "System.Void .ctor(System.Single)")]
        [MonoModForceCall]
        [MonoModRemove]
        public extern void ctor(float cannotHoldDelay = 0.1f);
        [MonoModConstructor]
        public void ctor() {
            ctor(0.1f);
        }

        public void SetSpeed(Vector2 speed) {
            SpeedSetter?.Invoke(speed);
        }
    }
}
