using Monocle;
using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Celeste.Mod.Entities
{
    /// <summary>
    /// Allows for Collision with any type in the game, similar to a PlayerCollider or PufferCollider.
    /// Performs the Action provided on collision. 
    /// </summary>
    /// <typeparam name="T">The specific type this component should try to collide with</typeparam>
    public abstract class CustomCollider<T> : Component
    {
        /// <summary>
        /// The Action invoked on Collision, with the object collided with passed as a parameter
        /// </summary>
        public Action<T> OnCollideAction;

        public Collider Collider;

        public CustomCollider(Action<T> onEntityAction, Collider collider = null)
            : base(active: true, visible: true)
        {
            OnCollideAction = onEntityAction;
            Collider = collider;
        }

        public override void Update()
        {
            if (OnCollideAction == null)
            {
                return;
            }

            Collider collider = Entity.Collider;
            if (Collider != null)
            {
                Entity.Collider = Collider;
            }

            foreach (T item in GetObjectsToCollide())
            {
                if (Entity.CollideCheck(GetEntityFromItem(item)))
                {
                    OnCollideAction(item);
                }
            }

            Entity.Collider = collider;
        }

        public override void DebugRender(Camera camera)
        {
            if (Collider != null)
            {
                Collider collider = Entity.Collider;
                Entity.Collider = Collider;
                Collider.Render(camera, Color.HotPink);
                Entity.Collider = collider;
            }
        }

        /// <summary>
        /// Used to obtain the objects this collider should try to collide with.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{T}"/> of Type <typeparamref name="T"/> with all
        /// items it will try to collide with each frame</returns>
        protected abstract IEnumerable<T> GetObjectsToCollide();

        /// <summary>
        /// Obtains the <see cref="Entity"/> from the paramter <paramref name="item"/> to run
        /// the <see cref="Entity.CollideCheck(Entity)"/> check on for collission.
        /// </summary>
        /// <param name="item">The current item being collision checked.</param>
        /// <returns>The <see cref="Entity"/> from <paramref name="item"/> being collision checked.</returns>
        protected abstract Entity GetEntityFromItem(T item);
    }
}
