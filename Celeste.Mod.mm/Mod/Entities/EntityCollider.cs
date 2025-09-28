using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Allows for Collision with any type of entity in the game, similar to a PlayerCollider or PufferCollider.
    /// Performs the Action provided on collision. 
    /// </summary>
    /// <typeparam name="T">The specific type of Entity this component should try to collide with</typeparam>
    public class EntityCollider<T> : CustomCollider<T> where T : Entity {
        public EntityCollider(Action<T> onEntityAction, Collider collider = null)
            : base(onEntityAction, collider)
        {
        }

        protected override Entity GetEntityFromItem(T item) => item;

        protected override IEnumerable<T> GetObjectsToCollide() {
            return (IEnumerable<T>) (Scene.Tracker as patch_Tracker).GetEntitiesTrackIfNeeded<T>();
        }
    }
}
