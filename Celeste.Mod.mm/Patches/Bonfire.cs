#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections.Generic;

namespace Celeste {
    class patch_Bonfire : Bonfire {

        private Sprite sprite;

        public patch_Bonfire(EntityData data, Vector2 offset) : base(data, offset) {
            //no-op, ignored by MonoMod
        }

        public extern void orig_ctor(Vector2 position, Mode mode);

        [MonoModConstructor]
        public void ctor(Vector2 position, Mode mode) {
            orig_ctor(position, mode);

            Dictionary<string, patch_Sprite.Animation> animations = ((patch_Sprite) sprite).Animations;
            if (animations.ContainsKey("startDream") && (animations["startDream"].Goto?[0].Equals("dreamy") ?? false) && !animations.ContainsKey("dreamy"))
                animations["startDream"].Goto = new Chooser<string>("burnDream"); // replace non-existent goto animation "dreamy" with correct animation
        }
    }
}