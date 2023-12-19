using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;

namespace Celeste {
    class patch_WaterInteraction : WaterInteraction {

        /// <summary>
        /// The water collision used for this component's Entity.
        /// </summary>
        public Rectangle Bounds {
            get {
                if (_bounds is Rectangle bounds) {
                    return new Rectangle((int) Entity.X + bounds.X, (int) Entity.Y + bounds.Y, bounds.Width, bounds.Height);
                } else
                    return new Rectangle((int) Entity.Center.X - 4, (int) Entity.Center.Y, 8, 16);
            }
        }
        private Rectangle? _bounds = null;


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
            ctor(isDashing);
            _bounds = bounds;
        }

    }
}
