#pragma warning disable CS0108 // Method hides inherited member
#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod;

namespace Celeste {
    [PatchSpeedInterface]
    class patch_TheoCrystal : TheoCrystal, ISpeed {
        public patch_Holdable Hold; // avoids extra cast

        [PatchInterfaceProperty]
        Vector2 ISpeed.Speed { get => Speed; set => Speed = value; }

        public patch_TheoCrystal(EntityData data, Vector2 offset) 
            : base(data, offset) {
        }

        public extern void orig_ctor(Vector2 position);

        [MonoModConstructor]
        public void ctor(Vector2 position) {
            orig_ctor(position);
            Hold.SpeedSetter = (speed) => { Speed = speed; };
        }
    }
}
