using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;

namespace Celeste {
    class patch_WaterInteraction : WaterInteraction {

        /// <summary>
        /// Check whether the component is interacting with a given water-type entity.
        /// </summary>
        /// <param name="water">The entity to check the interaction with.</param>
        /// <returns>Whether the component is interacting with the water.</returns>
        public bool Check(Entity water) {
            Collider normalCollider = Entity.Collider;
            if (_collider != null) {
                Entity.Collider = _collider;
            }
            bool result = water.CollideCheck(Entity);
            Entity.Collider = normalCollider;
            return result;
        }

        public Vector2 AbsoluteCenter => Entity.Position + (_collider ?? Entity.Collider).Center;

        private Collider _collider;

        /// <summary>
        /// The absolute rectangular bounds around the water collision used for this component's Entity.
        /// </summary>
        public Rectangle Bounds {
            get {
                Collider normalCollider = Entity.Collider;
                if (_collider != null) {
                    Entity.Collider = _collider; // linking collider to entity makes the position absolute
                }
                Rectangle result = Entity.Collider.Bounds;
                Entity.Collider = normalCollider;
                return result;
            }
        }


        /// <summary>
        /// Create a new <see cref="WaterInteraction"/>.
        /// </summary>
        /// <param name="bounds">The collision size.</param>
        /// <param name="isDashing">Used to determine the force of impact against the <see cref="T:Celeste.Water" />.</param>
        public patch_WaterInteraction(Rectangle bounds, Func<bool> isDashing) 
            : base(isDashing) {
            // no-op.
        }

        [MonoModIgnore] // Ignore this...
        [MonoModConstructor] // ...but make sure MonoMod treats it as a constructor.
        public extern void ctor(Func<bool> isDashing);

        [MonoModConstructor]
        public void ctor(Rectangle bounds, Func<bool> isDashing) {
            ctor(new Hitbox(bounds.Width, bounds.Height, bounds.X, bounds.Y), isDashing);
        }

        [MonoModConstructor]
        public void ctor(Collider collider, Func<bool> isDashing) {
            ctor(isDashing);
            _collider = collider;
        }

    }
}
