using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Semi-internal entity that removes any existing SpaceControllers.
    /// </summary>
    public class SpaceControllerBlocker : Entity {

        public override void Added(Scene scene) {
            base.Added(scene);
            scene.Entities.Remove(scene.Entities.OfType<SpaceController>());
        }

    }
}
