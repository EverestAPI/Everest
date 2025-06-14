using Celeste;
using System;
using MonoMod;
using Celeste.Mod.Registry;
using System.ComponentModel;

namespace Monocle {
    class patch_Entity : Entity {
        public new Scene Scene {
            [MonoModIgnore]
            get;
            [MonoModIgnore]
            private set;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        private EntityData _sourceData;
        
        /// <summary>
        /// EntityData instance used to construct this entity, might be null.
        /// </summary>
        public EntityData SourceData {
            get => _sourceData;
            set {
                // Notify the entity registry about our new data,
                // so it can properly track which C# types can be instantiated by a sid.
                if (value != null && value != _sourceData)
                    EntityRegistry.RegisterSidToTypeConnection(value.Name, GetType());
                
                _sourceData = value;
            }
        }

        /// <summary>
        /// EntityID used to construct this entity, might be default(EntityID).
        /// </summary>
        public EntityID SourceId { get; set; }

        public event Action<Entity> PreUpdate;
        public event Action<Entity> PostUpdate;

        internal void DissociateFromScene() {
            Scene = null;
        }

        internal void _PreUpdate() => PreUpdate?.Invoke(this);

        internal void _PostUpdate() => PostUpdate?.Invoke(this);
    }
}
