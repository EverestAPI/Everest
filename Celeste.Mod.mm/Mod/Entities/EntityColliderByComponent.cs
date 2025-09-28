using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Allows for Collision with any type of entity in the game, similar to a PlayerCollider or PufferCollider.
    /// Collision is done by component, as in, it will get all the components of the type and try to collide with their entities.
    /// Performs the Action provided on collision. 
    /// </summary>
    /// <typeparam name="T">The specific type of Component this component should try to collide with</typeparam>
    public class EntityColliderByComponent<T> : CustomCollider<T> where T : Component {
        public EntityColliderByComponent(Action<T> onEntityAction, Collider collider = null)
            : base(onEntityAction, collider)
        {
        }

        protected override Entity GetEntityFromItem(T item) => item.Entity;
        

        protected override IEnumerable<T> GetObjectsToCollide() {
            return (IEnumerable<T>) (Scene.Tracker as patch_Tracker).GetComponentsTrackIfNeeded<T>();
        }
    }
}
