using Monocle;
using System;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Mark this entity as a Custom <see cref="CutsceneEntity"/> or other Event <see cref="Entity"/>.
    /// <br></br>
    /// This Entity will be added when a matching Event ID is triggered.
    /// <br></br>
    /// <seealso href="https://github.com/EverestAPI/Resources/wiki/Creating-Custom-Events#customevent-attribute">Read More.</seealso>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class CustomEventAttribute : Attribute {

        /// <summary>
        /// A list of unique identifiers for this Event.<br/>
        /// Follows the pattern "ID [= LoadMethodName]".
        /// </summary>
        public string[] IDs;

        /// <summary>
        /// Mark this entity as a Custom <see cref="CutsceneEntity"/> or other Event <see cref="Entity"/>.
        /// </summary>
        /// <param name="ids">
        /// A list of unique identifiers for this Event.<br/>
        /// Follows the pattern "ID [= LoadMethodName]".
        /// </param>
        public CustomEventAttribute(params string[] ids) {
            IDs = ids;
        }
    }
}
