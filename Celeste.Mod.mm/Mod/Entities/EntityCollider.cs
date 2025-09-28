using Monocle;
using System;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Allows for collision with any type in the game, similar to a <see cref="PlayerCollider"/> or <see cref="PufferCollider"/>,
    /// but on all objects of type <typeparamref name="T"/>.<br/>
    /// Performs the <see cref="Action{T}"/> provided on collision. 
    /// </summary>
    /// <typeparam name="T">The specific type of <see cref="Entity"/> this <see cref="EntityCollider{T}"/> should try to collide with.</typeparam>
    public class EntityCollider<T> : Component where T : Entity {
        /// <summary>
        /// The <see cref="Action{T}"/> invoked on collision, with the object collided with passed as a parameter
        /// </summary>
        public Action<T> OnEntityAction;

        public Collider Collider;

        public EntityCollider(Action<T> onEntityAction, Collider collider = null)
            : base(active: true, visible: true) {
            OnEntityAction = onEntityAction;
            Collider = collider;
        }

        public override void Update() {
            if (OnEntityAction == null) {
                return;
            }

            Collider collider = Entity.Collider;
            if (Collider != null) {
                Entity.Collider = Collider;
            }

            foreach (Entity item in (Scene.Tracker as patch_Tracker).GetEntitiesTrackIfNeeded<T>()) {
                if (Entity.CollideCheck(item)) {
                    OnEntityAction(item as T);
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
