using Monocle;
using System;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Allows for collision with any type in the game, similar to a <see cref="PlayerCollider"/> or <see cref="PufferCollider"/>,
    /// but on all objects of type <typeparamref name="T"/>.<br/>
    /// Performs the <see cref="Action{T}"/> provided on collision. 
    /// Collision is done by <see cref="Component"/>, as in,
    /// it will get all the components of type <typeparamref name="T"/> and try to collide with their entities.
    /// </summary>
    /// <typeparam name="T">The specific type of <see cref="Component"/> this <see cref="EntityColliderByComponent{T}"/> should try to collide with.</typeparam>
    public class EntityColliderByComponent<T> : Component where T : Component {
        /// <summary>
        /// The <see cref="Action{T}"/> invoked on collision, with the object collided with passed as a parameter
        /// </summary>
        public Action<T> OnComponentAction;

        public Collider Collider;

        public EntityColliderByComponent(Action<T> onComponentAction, Collider collider = null)
            : base(active: true, visible: true) {
            OnComponentAction = onComponentAction;
            Collider = collider;
        }

        public override void Update() {
            if (OnComponentAction == null) {
                return;
            }

            Collider collider = Entity.Collider;
            if (Collider != null) {
                Entity.Collider = Collider;
            }

            foreach (Component item in (Scene.Tracker as patch_Tracker).GetComponentsTrackIfNeeded<T>()) {
                if (Entity.CollideCheck(item.Entity)) {
                    OnComponentAction(item as T);
                }
            }

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
