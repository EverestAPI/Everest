#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Microsoft.Xna.Framework;
using MonoMod;

namespace Monocle {
    class patch_Hitbox : Hitbox {
        [MonoModIgnore]
        private float width;
        [MonoModIgnore]
        private float height;
        
        public patch_Hitbox(float width, float height, float x = 0, float y = 0) : base(width, height, x, y)
        {
            // no-op, ignored by MonoMod
        }
        
        // Seal and add AggressiveInlining onto the properties for better performance in various collision checks.
        // No mods override these properties (nor should there be a reason to do so) as of now, so this is safe to do.
        
        [MonoModIgnore, ForceSealed, ForceAggressiveInlining]
        public override float Width { get; set; }
        
        [MonoModIgnore, ForceSealed, ForceAggressiveInlining]
        public override float Height { get; set; }

        [MonoModIgnore, ForceSealed, ForceAggressiveInlining]
        public override float Left { get; set; }

        [MonoModIgnore, ForceSealed, ForceAggressiveInlining]
        public override float Top { get; set; }

        [MonoModIgnore, ForceSealed, ForceAggressiveInlining]
        public override float Right { get; set; }

        [MonoModIgnore, ForceSealed, ForceAggressiveInlining]
        public override float Bottom { get; set; }
        
        // Optimise most frequent collision methods to avoid virtual calls:
        
        [MonoModReplace]
        public override bool Collide(Rectangle rect) {
            var pos = AbsolutePosition;
            
            return pos.X + width > rect.Left
                && pos.Y + height > rect.Top
                && pos.X < rect.Right
                && pos.Y < rect.Bottom;
        }
        
        [MonoModReplace]
        public bool Intersects(patch_Hitbox hitbox)
        {
            var pos = AbsolutePosition;
            var otherPos = hitbox.AbsolutePosition;
            
            return pos.X + width > otherPos.X
                && pos.Y + height > otherPos.Y
                && pos.X < otherPos.X + hitbox.width
                && pos.Y < otherPos.Y + hitbox.height;
        }
        
        [MonoModReplace]
        public bool Intersects(float x, float y, float width, float height)
        {
            var pos = AbsolutePosition;
            
            return pos.X + this.width > x
                && pos.Y + this.height > y
                && pos.X < x + width
                && pos.Y < y + height;
        }
    }
}
