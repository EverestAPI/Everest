using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_Puffer : Puffer {
        public patch_Puffer(EntityData data, Vector2 offset) : base(data, offset) {
            // ignored by MonoMod
        }

        [MonoModLinkTo("Monocle.Entity", "Added")]
        [MonoModIgnore]
        public extern void base_Added(Scene scene);

        public override void Added(Scene scene) {
            base_Added(scene);
            if (Depth == 0 && ((patch_AreaKey) (object) (scene as Level).Session.Area).LevelSet != "Celeste") {
                Depth = -1; // makes puffer boosts work after player respawn or other depth reset
            }
        }
    }
}