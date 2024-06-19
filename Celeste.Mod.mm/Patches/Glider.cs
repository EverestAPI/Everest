#pragma warning disable CS0108 // Method hides inherited member
#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Microsoft.Xna.Framework;
using MonoMod;

namespace Celeste {
    class patch_Glider : Glider {
        public patch_Holdable Hold; // avoids extra cast

        public patch_Glider(Vector2 position, bool bubble, bool tutorial)
            : base(position, bubble, tutorial) {
        }

        public extern void orig_ctor(Vector2 position, bool bubble, bool tutorial);

        [MonoModConstructor]
        public void ctor(Vector2 position, bool bubble, bool tutorial) {
            orig_ctor(position, bubble, tutorial);
            Hold.SpeedSetter = (speed) => { Speed = speed; };
        }
    }
}