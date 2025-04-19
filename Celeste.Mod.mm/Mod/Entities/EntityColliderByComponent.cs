using Monocle;
using System;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Allows for Collision with any type of entity in the game, similar to a PlayerCollider or PufferCollider.
    /// Collision is done by component, as in, it will get all the components of the type and try to collide with their entities.
    /// Performs the Action provided on collision. 
    /// </summary>
    /// <typeparam name="T">The specific type of Component this component should try to collide with</typeparam>
    public class EntityColliderByComponent<T> : Component where T : Component {
        /// <summary>
        /// The Action invoked on Collision, with the Component collided with passed as a parameter
        /// </summary>
        public Action<T> OnComponentAction;

        public Collider Collider;

        public EntityColliderByComponent(Action<T> onComponentAction, Collider collider = null)
            : base(active: true, visible: true) {
            OnComponentAction = onComponentAction;
            Collider = collider;
        }

        public override void Added(Entity entity) {
            base.Added(entity);
            //Only called if Component is added post Scene Begin and Entity Adding and Awake time.
            if (Scene != null) {
                if (!Scene.Tracker.IsComponentTracked<T>()) {
                    patch_Tracker.AddTypeToTracker(typeof(T));
                }
                patch_Tracker.Refresh(Scene);
            }
        }

        public override void EntityAdded(Scene scene) {
            if (!scene.Tracker.IsComponentTracked<T>()) {
                patch_Tracker.AddTypeToTracker(typeof(T));
            }
            base.EntityAdded(scene);
        }

        public override void EntityAwake() {
            patch_Tracker.Refresh(Scene);
        }

        public override void Update() {
            if (OnComponentAction == null) {
                return;
            }

            Collider collider = Entity.Collider;
            if (Collider != null) {
                Entity.Collider = Collider;
            }

            Entity.CollideDoByComponent(OnComponentAction);

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
