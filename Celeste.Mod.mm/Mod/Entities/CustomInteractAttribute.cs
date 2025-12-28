using Monocle;
using System;

namespace Celeste.Mod.Entities {
#nullable enable
    /// <summary>
    /// Mark this entity as a custom <see cref="CutsceneEntity"/> or other Event <see cref="Entity"/>,
    /// for use with an <see cref="InteractTrigger"/>.
    /// <br/>
    /// This entity will be added when a matching Event ID is interacted with.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class CustomInteractAttribute : Attribute {

        /// <summary>
        /// A list of unique identifiers for this Interact Event.
        /// </summary>
        public string[] IDs;

        /// <summary>
        /// Whether to set an appropriate Session flag and progress the trigger's event index on interaction.<br/>
        /// Default value is <see langword="true"/>.<br/>
        /// This is ignored if the constructor / generator method has the parameter `<see langword="ref"/> <see cref="bool"/> progressEvent`.
        /// </summary>
        public bool ProgressEvent = true;

        /// <summary>
        /// Mark this entity as a custom <see cref="CutsceneEntity"/> or other Event <see cref="Entity"/>,
        /// for use with an <see cref="InteractTrigger"/>.
        /// </summary>
        /// <param name="ids">A list of unique identifiers for this Interact Event.</param>
        public CustomInteractAttribute(params string[] ids) {
            IDs = ids;
        }
    }
}
