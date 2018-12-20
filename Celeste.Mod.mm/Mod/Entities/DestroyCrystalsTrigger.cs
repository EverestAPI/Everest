using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Entities {
    public class DestroyCrystalsTrigger : Trigger {

        private DestroyTypes destructionType;

        public DestroyCrystalsTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
            destructionType = data.Enum("destroyEveryCrystal", DestroyTypes.InTrigger);
        }

        public override void OnEnter(Player player) {
            base.OnEnter(player);

            foreach (CrystalStaticSpinner spinner in Scene.Entities.OfType<CrystalStaticSpinner>())
                if (CollideCheck(spinner) || destructionType == DestroyTypes.EveryCrystal)
                    spinner.Destroy();

            RemoveSelf();
        }

        public enum DestroyTypes {
            InTrigger,
            EveryCrystal,
        }

    }
}
