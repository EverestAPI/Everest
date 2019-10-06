using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.Entities {
    [CustomEntity("everest/lavaBlockerTrigger")]
    [CustomEntity("cavern/lavablockertrigger")]
    public class LavaBlockerTrigger : Trigger {
        List<DynData<RisingLava>> risingLavas;
        List<DynData<SandwichLava>> sandwichLavas;
        private readonly bool canReenter;
        private bool enabled = true;

        public LavaBlockerTrigger(EntityData data, Vector2 offset) : base(data, offset) {
            canReenter = data.Bool("canReenter", false);
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);

            risingLavas = scene.Entities.OfType<RisingLava>().Select(lava => new DynData<RisingLava>(lava)).ToList();
            sandwichLavas = scene.Entities.OfType<SandwichLava>().Select(lava => new DynData<SandwichLava>(lava)).ToList();
        }

        public override void OnStay(Player player) {
            if (!enabled)
                return;

            foreach (DynData<RisingLava> data in risingLavas)
                if (data.IsAlive)
                    data.Set("waiting", true);
            foreach (DynData<SandwichLava> data in sandwichLavas)
                if (data.IsAlive)
                    data.Set("Waiting", true);
        }

        public override void OnLeave(Player player) {
            base.OnLeave(player);

            if (!canReenter)
                enabled = false;
        }
    }
}
