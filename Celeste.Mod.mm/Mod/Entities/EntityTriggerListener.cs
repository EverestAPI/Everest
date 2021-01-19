using Monocle;
using System;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Allows an Entity to be triggered using an EntityTrigger.
    /// </summary>
    [Tracked]
    public class EntityTriggerListener : Component {

        public Action OnTrigger;
        public Action OnStartTriggered;

        /// <summary>
        /// Create a new EntityTriggerListener component.
        /// </summary>
        /// <param name="onTrigger">Called when the Player enters an EntityTrigger that has this entity in its range.<br></br>
        /// May be called more than once if multiple EntityTriggers are present.</param>
        /// <param name="onStartTriggered">Called if a persistent EntityTrigger that has this entity in its range has its flag already set.<br></br>
        /// May be called more than once if multiple EntityTriggers are present.</param>
        public EntityTriggerListener(Action onTrigger, Action onStartTriggered) 
            : base(false, false) {
            OnTrigger = onTrigger;
            OnStartTriggered = onStartTriggered;
        }
    }
}
