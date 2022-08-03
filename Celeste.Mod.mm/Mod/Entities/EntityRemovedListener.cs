using Monocle;
using System;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Allows for defining an Action to take when an Entity is removed.
    /// </summary>
    public class EntityRemovedListener : Component {

        /// <summary>
        /// Called when this Entity is removed from the scene.
        /// </summary>
        public Action OnEntityRemoved;

        /// <summary>
        /// Create a new EntityRemovedListener component.
        /// </summary>
        /// <param name="onEntityRemoved">Called when this Entity is removed from the scene.</param>
        public EntityRemovedListener(Action onEntityRemoved) 
            : base(false, false) {
            OnEntityRemoved = onEntityRemoved;
        }

        public override void EntityRemoved(Scene scene) {
            base.EntityRemoved(scene);
            OnEntityRemoved?.Invoke();
        }

    }
}
