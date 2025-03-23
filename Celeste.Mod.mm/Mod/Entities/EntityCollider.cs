using Monocle;
using System;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Allows for Collision with any type of entity in the game, similar to a PlayerCollider or PufferCollider.
    /// Performs the Action provided on collision. 
    /// </summary>
    /// <typeparam name="T">The specific type of Entity this component should try to collide with</typeparam>
    public class EntityCollider<T> : Component where T : Entity {
        /// <summary>
        /// The Action invoked on Collision, with the Entity collided with passed as a parameter
        /// </summary>
        public Action<T> OnEntityAction;

        public Collider Collider;

        public EntityCollider(Action<T> onEntityAction, Collider collider = null)
            : base(active: true, visible: true) {
            OnEntityAction = onEntityAction;
            Collider = collider;
        }

        public override void Added(Entity entity) {
            base.Added(entity);
            //Only called if Component is added post Scene Begin and Entity Adding and Awake time.
            if (Scene != null) {
                if (!Scene.Tracker.IsEntityTracked<T>()) {
                    patch_Tracker.AddTypeToTracker(typeof(T));
                }
                patch_Tracker.Refresh(Scene);
            }
        }

        public override void EntityAdded(Scene scene) {
            if (!scene.Tracker.IsEntityTracked<T>()) {
                patch_Tracker.AddTypeToTracker(typeof(T));
            }
            base.EntityAdded(scene);
        }

        public override void EntityAwake() {
            patch_Tracker.Refresh(Scene);
        }

        public override void Update() {
            if (OnEntityAction == null) {
                return;
            }

            Collider collider = Entity.Collider;
            if (Collider != null) {
                Entity.Collider = Collider;
            }

            Entity.CollideDo(OnEntityAction);

            Entity.Collider = collider;
        }

        public override void DebugRender(Camera camera) {
            if (Collider != null) {
                Collider collider = Entity.Collider;
                Entity.Collider = Collider;
                Collider.Render(camera, Color.HotPink);
                Entity.Collider = collider;
            }
        }
    }
}
