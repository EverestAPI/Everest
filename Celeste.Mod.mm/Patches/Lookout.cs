#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // Field is never used

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_Lookout : Lookout {

        private bool interacting;

        public patch_Lookout(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchLookoutUpdate]
        public override extern void Update();

        // keep for backward compatibility
        public override void SceneEnd(Scene scene) {
            base.SceneEnd(scene);
        }
    }
}