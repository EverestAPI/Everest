#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;

namespace Celeste {
    class patch_RumbleTrigger : RumbleTrigger {

        private bool manualTrigger;
        private float left;
        private float right;

        private bool constrainHeight;
        private float top;
        private float bottom;

        public patch_RumbleTrigger(EntityData data, Vector2 offset, EntityID id) : base(data, offset, id) {
            // no-op.
        }

        public extern void orig_ctor(EntityData data, Vector2 offset, EntityID id);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset, EntityID id) {
            orig_ctor(data, offset, id);

            constrainHeight = data.Bool("constrainHeight");

            Vector2[] nodes = data.NodesOffset(offset);
            if (nodes.Length >= 2) {
                top = Math.Min(nodes[0].Y, nodes[1].Y);
                bottom = Math.Max(nodes[0].Y, nodes[1].Y);
            }
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchRumbleTriggerAwake] // ... except for manually manipulating the method via MonoModRules
        public extern override void Awake(Scene scene);

        public static void ManuallyTrigger(Vector2 position, float delay, bool triggerUnconstrained = true) {
            foreach (patch_RumbleTrigger rumbleTrigger in Engine.Scene.Entities.FindAll<patch_RumbleTrigger>()) {
                if (rumbleTrigger.manualTrigger && position.X >= rumbleTrigger.left && position.X <= rumbleTrigger.right) {
                    if (rumbleTrigger.constrainHeight) {
                        if (position.Y >= rumbleTrigger.top && position.Y <= rumbleTrigger.bottom)
                            rumbleTrigger.Invoke(delay);
                    } else if (triggerUnconstrained)
                        rumbleTrigger.Invoke(delay);
                }
            }
        }

        [MonoModIgnore]
        public extern void Invoke(float delay);

    }
}
