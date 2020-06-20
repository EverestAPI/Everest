namespace Celeste.Mod.Entities {
    /// <summary>
    /// Allows an Entity to be triggered using an EntityTrigger.
    /// </summary>
    public interface ITriggerable {
        /// <summary>
        /// Called when the Player enters an EntityTrigger that has this entity in its range.
        /// This method may be called more than once if multiple EntityTriggers are present.
        /// </summary>
        void Trigger();

        /// <summary>
        /// Called if a persistent EntityTrigger that has this entity in its range has its flag already set.
        /// This method may be called more than once if multiple EntityTriggers are present.
        /// </summary>
        void StartTriggered();

    }
}
