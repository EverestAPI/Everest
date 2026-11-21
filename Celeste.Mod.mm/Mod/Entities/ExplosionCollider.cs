using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Linq;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Allows defining an Action to be called when an explosion that calls <see cref="ExplosionCollider.TriggerExplosionColliders"/>, e.g. a puffer or seeker explosion, collides with an entity.
    /// </summary>
    [Tracked]
    public class ExplosionCollider : Component {
        /// <summary>
        /// Called when an explosion collides with this component.<br/>
        /// The first argument is the entity triggering the explosion, and the second argument is the position of the origin of the explosion.
        /// </summary>
        public Action<Entity, Vector2> OnCollide;

        /// <summary>
        /// If not null, used instead of this component's entity's collider for colliding with explosions.
        /// </summary>
        public Collider Collider;

        /// <summary>
        /// Create a new <see cref="ExplosionCollider"/> component.
        /// </summary>
        /// <param name="onCollide">The Action to call when an explosion collides with this component.</param>
        /// <param name="collider">A collider to optionally use instead of this component's entity's collider for colliding with explosions.</param>
        public ExplosionCollider(Action<Entity, Vector2> onCollide, Collider collider = null) : base(false, false) {
            OnCollide = onCollide;
            Collider = collider;
        }

        public bool Check(Entity other) {
            Collider entityCollider = Entity.Collider;
            if (Collider != null) {
                Entity.Collider = Collider;
            }

            bool result = other.CollideCheck(Entity);

            Entity.Collider = entityCollider;

            return result;
        }

        public override void DebugRender(Camera camera) {
            if (Collider != null) {
                Collider entityCollider = Entity.Collider;
                Entity.Collider = Collider;
                Collider.Render(camera, Color.Orange * (Entity.Collidable ? 1f : 0.5f));
                Entity.Collider = entityCollider;
            }
        }

        /// <summary>
        /// Triggers all <see cref="ExplosionCollider"/>s currently colliding with the specified entity.
        /// (Does not launch Players or TheoCrystals.)
        /// </summary>
        /// <param name="entity">The entity triggering the explosion.</param>
        /// <param name="explosionOrigin">The position of the origin of the explosion. If null, the center of the entity triggering the explosion is used.</param>
        public static void TriggerExplosionColliders(Entity entity, Vector2? explosionOrigin = null) {
            // collect the colliding components beforehand so no side effects from their `OnCollide` will affect which components are checked
            ExplosionCollider[] collidingExplosionColliders = entity.Scene.Tracker
                .GetComponents<ExplosionCollider>()
                .Cast<ExplosionCollider>()
                .Where(c => c.OnCollide != null && c.Check(entity))
                .ToArray();

            Vector2 origin = explosionOrigin ?? entity.Center;

            foreach (ExplosionCollider explosionCollider in collidingExplosionColliders) {
                explosionCollider.OnCollide(entity, origin);
            }

            Everest.Events.ExplosionCollider.TriggerExplosionColliders(entity, origin);
        }
    }
}

namespace Celeste.Mod {
    public static partial class Everest {
        public static partial class Events {
            public static class ExplosionCollider {
                public delegate void TriggerExplosionCollidersHandler(Entity entity, Vector2 explosionOrigin);
                /// <summary>
                /// Called during the ExplosionCollider.TriggerExplosionColliders method called by e.g. puffer/seeker explosions.
                /// </summary>
                public static event TriggerExplosionCollidersHandler OnTriggerExplosionColliders;
                internal static void TriggerExplosionColliders(Entity entity, Vector2 explosionOrigin)
                    => OnTriggerExplosionColliders?.Invoke(entity, explosionOrigin);
            }
        }
    }
}
