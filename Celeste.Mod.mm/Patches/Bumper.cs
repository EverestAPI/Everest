#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_Bumper : Bumper {

        private Vector2 anchor;
        private SineWave sine;

        public patch_Bumper(EntityData data, Vector2 offset) : base(data, offset) {
            //no-op, ignored by MonoMod
        }

        [MonoModReplace]
        private void UpdatePosition() {
            Position = new Vector2((float) ((double) anchor.X + sine.Value * 3.0), (float) ((double) anchor.Y + sine.ValueOverTwo * 2.0));
        }

    }
}