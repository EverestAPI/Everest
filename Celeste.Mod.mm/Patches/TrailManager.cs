using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste
{
    class patch_TrailManager : TrailManager {

        // Make this signature accessible to older mods.
        public static void Add(Entity entity, Color color, float duration = 1f) {
            TrailManager.Add(entity, color, duration);
        }

    }
}
