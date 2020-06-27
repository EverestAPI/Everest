#pragma warning disable CS0414 // The field 'patch_IntroCrusher.triggered' is assigned but its value is never used
#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Collections;
using System.Linq;

namespace Celeste {
    // : Solid because base.Added
    class patch_IntroCrusher : Solid {

        // We're effectively in IntroCrusher, but still need to "expose" private fields to our mod.
        private Vector2 end;
        private TileGrid tilegrid;

        public string levelFlags;

        private bool manualTrigger;
        private float delay;
        private bool triggered;

        private float speed;

        public patch_IntroCrusher(Vector2 position, int width, int height, Vector2 node)
            : base(position, width, height, true) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(EntityData data, Vector2 offset);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset) {
            orig_ctor(data, offset);

            levelFlags = data.Attr("flags");

            manualTrigger = data.Bool("manualTrigger");
            delay = data.Float("delay", 1.2f);

            speed = data.Float("speed", 2f);

            string tiletype = data.Attr("tiletype");
            if (!string.IsNullOrEmpty(tiletype)) {
                Remove(tilegrid);
                Add(tilegrid = GFX.FGAutotiler.GenerateBox(tiletype[0], data.Width / 8, data.Height / 8).TileGrid);
            }

            Add(new EntityTriggerListener(Trigger, StartTriggered));
        }

        public extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            Level level = scene as Level;
            if (level.Session.Area.GetLevelSet() == "Celeste" || string.IsNullOrEmpty(levelFlags)) {
                orig_Added(scene);
                return;
            }

            base.Added(scene);

            if (levelFlags.Split(',').Any(flag => level.Session.GetLevelFlag(flag))) {
                Position = end;
            } else {
                Add(new Coroutine(Sequence(), true));
            }
        }

        public void Trigger() {
            if (manualTrigger)
                triggered = true;
        }

        public void StartTriggered() {
            if (manualTrigger) {
                triggered = true;
                Position = end;
                Remove(Get<Coroutine>());
            }
        }

        [MonoModIgnore]
        [PatchIntroCrusherSequence]
        private extern IEnumerator Sequence();

    }
}
