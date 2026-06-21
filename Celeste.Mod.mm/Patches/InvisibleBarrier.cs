using Microsoft.Xna.Framework;
using MonoMod;

namespace Celeste {
    class patch_InvisibleBarrier : InvisibleBarrier {

        [MonoModConstructor]
        [MonoModReplace]
        public patch_InvisibleBarrier(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Width, data.Height) {

            if (data.Bool("allowClimbing", false))
                Remove(Get<ClimbBlocker>());

            SurfaceSoundIndex = data.Int("surfaceIndex", SurfaceSoundIndex);
        }

    }
}
