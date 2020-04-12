#pragma warning disable CS0626 // Method / operator / getter is marked external and has no attribute

using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste {
    public class patch_CrystalDebris : CrystalDebris {
        // expose this private field to our patch.
        private Vector2 speed;

        public extern void orig_Update();
        public override void Update() {
            // clamp the debris speed to avoid almost freezing the game when debris get stuck in objects
            // see https://github.com/EverestAPI/Everest/issues/132
            speed.X = Calc.Clamp(speed.X, -100000, 100000);
            speed.Y = Calc.Clamp(speed.Y, -100000, 100000);

            orig_Update();
        }
    }
}
