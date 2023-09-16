﻿using System;
using MonoMod;
using Celeste.Mod.Entities;
using Celeste;

namespace Monocle {
    class patch_Entity : Entity {
        public new Scene Scene {
            [MonoModIgnore]
            get;
            [MonoModIgnore]
            private set;
        }

        public EntityData __EntityData;
        public event Action<Entity> PreUpdate;
        public event Action<Entity> PostUpdate;

        internal void DissociateFromScene() {
            Scene = null;
        }

        internal void _PreUpdate() => PreUpdate?.Invoke(this);

        internal void _PostUpdate() => PostUpdate?.Invoke(this);
    }
}
