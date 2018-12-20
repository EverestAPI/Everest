using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Entities {
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

            foreach (CrystalStaticSpinner spinner in Scene.Entities.OfType<CrystalStaticSpinner>())
                if (CollideCheck(spinner) || mode == Modes.All)
                    spinner.Destroy();

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
