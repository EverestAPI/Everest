using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_FakeWall : FakeWall {
        private bool disableTransitionFading;

        public patch_FakeWall(EntityID eid, EntityData data, Vector2 offset, Modes mode) : base(eid, data, offset, mode) {
            // dummy constructor
        }

        [MonoModConstructor]
        public extern void orig_ctor(EntityID eid, EntityData data, Vector2 offset, Modes mode);

        [MonoModConstructor]
        public void ctor(EntityID eid, EntityData data, Vector2 offset, Modes mode) {
            orig_ctor(eid, data, offset, mode);

            disableTransitionFading = data.Bool("disableTransitionFading", false);
        }

        [MonoModLinkTo("Monocle.Entity", "System.Void Awake(Monocle.Scene)")]
        [MonoModRemove]
        public extern void base_Awake(Scene scene);

#pragma warning disable CS0626 // external method with no attribute
        public extern void orig_Awake(Scene scene);
#pragma warning restore CS0626

        public override void Awake(Scene scene) {
            if (disableTransitionFading && !CollideCheck<Player>()) {
                // if not colliding with the player, Awake() only calls the base method and adds the transition listener to make the fake wall fade in.
                // since disableTransitionFading is enabled, we don't want this, so only call the base method;
                base_Awake(scene);
            } else {
                orig_Awake(scene);
            }
        }
    }
}
