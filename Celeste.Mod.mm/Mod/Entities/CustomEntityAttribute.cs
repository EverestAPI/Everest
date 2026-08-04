using Monocle;
using System;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Mark this entity as a Custom <see cref="Entity"/> or <see cref="Trigger"/>.
    /// <br></br>
    /// This Entity will be loaded when a matching ID is detected.
    /// <br></br>
    /// <seealso href="https://github.com/EverestAPI/Resources/wiki/Custom-Entities-and-Triggers#customentity">Read More.</seealso>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class CustomEntityAttribute : Attribute {

        /// <summary>
        /// A list of unique identifiers for this Entity.<br/>
        /// Follows the pattern "ID [= LoadMethodName]".
        /// </summary>
        public string[] IDs;

        /// <summary>
        /// Mark this entity as a Custom <see cref="Entity"/> or <see cref="Trigger"/>.
        /// </summary>
        /// <param name="ids">
        /// A list of unique identifiers for this Entity.<br/>
        /// Follows the pattern "ID [= LoadMethodName]".
        /// </param>
        public CustomEntityAttribute(params string[] ids) {
            IDs = ids;
        }
    }
}
