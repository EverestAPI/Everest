using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// <inheritdoc/><br/>
    /// Collision is done by <see cref="Component"/>, as in,
    /// it will get all the components of type <typeparamref name="T"/> and try to collide with their entities.
    /// </summary>
    /// <typeparam name="T">The specific type of <see cref="Component"/> this <see cref="CustomCollider{T}"/> should try to collide with.</typeparam>
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
