using Monocle;
using System.Linq;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Semi-internal entity that removes any existing SpaceControllers.
    /// </summary>
    [CustomEntity("everest/spaceControllerBlocker")]
    public class SpaceControllerBlocker : Entity {

        public override void Added(Scene scene) {
            base.Added(scene);
            scene.Entities.Remove(scene.Entities.OfType<SpaceController>());
        }

    }
}
