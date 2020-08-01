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
                if (_bounds != null) {
                    return new Rectangle((int) Entity.X + _bounds.X, (int) Entity.Y + _bounds.Y, _bounds.Width, _bounds.Height);
                } else
                    return new Rectangle((int) Entity.Center.X - 4, (int) Entity.Center.Y, 8, 16);
            }
        }
        private Rectangle _bounds;


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
