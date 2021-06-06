using Microsoft.Xna.Framework;
using MonoMod;
using System;

namespace Celeste {
    class patch_DashListener : DashListener {

        // Dealing with constructors is wack
        [MonoModLinkTo("Monocle.Component", "System.Void .ctor(System.Boolean,System.Boolean)")]
        [MonoModRemove]
        public extern void base_ctor(bool active, bool visible);
        [MonoModConstructor]
        public void ctor(Action<Vector2> onDash) {
            base_ctor(false, false);
            OnDash = onDash;
        }

        /// <summary>
        /// <inheritdoc cref="DashListener.DashListener"/>
        /// </summary>
        /// <param name="onDash">Invoked when the <see cref="Player"/> dashes.</param>
        public patch_DashListener(Action<Vector2> onDash) : base() {
            // no-op. used for documentation purposes only.
        }

    }
}
