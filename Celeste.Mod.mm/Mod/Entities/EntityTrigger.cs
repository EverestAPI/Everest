using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.Entities {
    [Tracked]
    [CustomEntity("everest/entityTrigger")]
    public class EntityTrigger : Trigger {
        private bool manualTrigger;
        private bool persistent;
        private EntityID id;

        private float left;
        private float right;
        private float top;
        private float bottom;
        private List<EntityTriggerListener> listeners;

        private bool triggered;

        public EntityTrigger(EntityData data, Vector2 offset, EntityID id) : base(data, offset) {
            manualTrigger = data.Bool("manualTrigger");
            persistent = data.Bool("persistent");
            this.id = id;

            listeners = new List<EntityTriggerListener>();

            Vector2[] array = data.NodesOffset(offset);
            if (array.Length >= 2) {
                left = Math.Min(array[0].X, array[1].X);
                right = Math.Max(array[0].X, array[1].X);
                top = Math.Min(array[0].Y, array[1].Y);
                bottom = Math.Max(array[0].Y, array[1].Y);
            }
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);

            bool alreadyTriggered = persistent && SceneAs<Level>().Session.GetFlag(id.ToString());

            foreach (EntityTriggerListener listener in scene.Tracker.GetComponents<EntityTriggerListener>()) {
                Entity entity = listener.Entity;
                if (entity.X >= left && entity.X <= right && entity.Y >= top && entity.Y <= bottom) { 
                    if (alreadyTriggered)
                        listener.OnStartTriggered?.Invoke();
                    else
                        listeners.Add(listener);
                }
            }

            if (alreadyTriggered)
                RemoveSelf();
        }

        public override void OnEnter(Player player) {
            base.OnEnter(player);
            if (manualTrigger)
                return;

            Invoke();
        }

        /// <summary>
        /// Trigger all <see cref="EntityTrigger"/>s that have <paramref name="point"/> within their range.
        /// </summary>
        /// <param name="point"></param>
        public static void ManuallyTrigger(Vector2 point) {
            foreach (EntityTrigger trigger in Engine.Scene.Tracker.GetEntities<EntityTrigger>()) {
                if (trigger.manualTrigger && point.X >= trigger.left && point.X <= trigger.right && point.Y >= trigger.top && point.Y <= trigger.bottom)
                    trigger.Invoke();
            }
        }

        private void Invoke() {
            if (!triggered) {
                triggered = true;
                if (persistent)
                    SceneAs<Level>().Session.SetFlag(id.ToString());

                foreach (EntityTriggerListener listener in listeners) {
                    listener.OnTrigger?.Invoke();
                }
            }
        }

    }
}
