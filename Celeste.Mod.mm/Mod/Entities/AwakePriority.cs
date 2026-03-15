using Monocle;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Allows setting the awake priority for an entity (similar to Depth, but decides the order of Awake instead of Update).
    ///
    /// Adding multiple AwakePriority components to an entity is undefined. For this reason it is better to use
    /// <see cref="patch_Entity.AwakePriority" />, which manages this for you.
    /// </summary>
    public class AwakePriority : Component {
        public int Priority;

        public AwakePriority(int priority)
            : base(active: false, visible: true) {
            Priority = priority;
        }
    }
}
