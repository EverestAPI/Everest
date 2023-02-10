using Microsoft.Xna.Framework;
using Monocle;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.Entities {
    [CustomEntity("everest/crystalShatterTrigger", "outback/destroycrystalstrigger")]
    public class CrystalShatterTrigger : Trigger {

        private Modes mode;

        public CrystalShatterTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
            if (data.Has("destroyEveryCrystal"))
                mode = (Modes) (int) data.Enum("destroyEveryCrystal", ModesLegacy.InTrigger);
            if (data.Has("mode"))
                mode = data.Enum("mode", Modes.Contained);
        }

        public override void OnEnter(Player player) {
            base.OnEnter(player);

            List<Entity> spinners = Scene.Tracker.GetEntities<CrystalStaticSpinner>();
            if (spinners.Count > 0) {
                if (mode == Modes.All)
                    Audio.Play("event:/game/06_reflection/boss_spikes_burst");

                foreach (CrystalStaticSpinner spinner in spinners) {
                    bool wasCollidable = spinner.Collidable;
                    spinner.Collidable = true;
                    if (mode == Modes.All || CollideCheck(spinner))
                        spinner.Destroy();
                    spinner.Collidable = wasCollidable;
                }
            }

            RemoveSelf();
        }

        public enum Modes {
            Contained,
            All,
        }

        private enum ModesLegacy {
            InTrigger,
            EveryCrystal,
        }

    }
}
