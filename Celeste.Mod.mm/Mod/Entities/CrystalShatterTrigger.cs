using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            List<CrystalStaticSpinner> spinners = Scene.Entities.OfType<CrystalStaticSpinner>().ToList();
            if (spinners.Count > 0) {
                if (mode == Modes.All)
                    Audio.Play("event:/game/06_reflection/boss_spikes_burst");
                    
                foreach (CrystalStaticSpinner spinner in spinners)
                    if (CollideCheck(spinner) || mode == Modes.All)
                        spinner.Destroy();
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
