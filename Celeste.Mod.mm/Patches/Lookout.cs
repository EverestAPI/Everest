#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections;

namespace Celeste {
    public class patch_Lookout : Lookout {

        // We're effectively in Lookout, but still need to "expose" private fields to our mod.
        private bool interacting;

        private bool onlyX;

        public patch_Lookout(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(EntityData data, Vector2 offset);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset) {
            orig_ctor(data, offset);

            onlyX = data.Bool("onlyX");
        }

        public override void SceneEnd(Scene scene) {
            base.SceneEnd(scene);
            if (interacting) {
                Player player = scene.Tracker.GetEntity<Player>();
                if (player != null) {
                    player.StateMachine.State = 0;
                    player.Sprite.Visible = player.Hair.Visible = true;
                }
            }
        }

        [MonoModIgnore] // don't change anything in the method...
        [PatchLookoutRoutine] // except for patching it through MonoModRules
        private extern IEnumerator LookRoutine(Player player);

        public class patch_Hud : Entity{
            public bool OnlyX;

            [MonoModIgnore] // don't change anything in the method...
            [PatchLookoutHudRender] // except for patching it through MonoModRules
            public extern override void Render();
        }

    }
}
