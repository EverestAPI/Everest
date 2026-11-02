using System;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Associate this entity with one or more IDs, without also registering a loader.<br/>
    /// Useful for internal entities that should be trackable by their SID, but not created via map data.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class AssociatedWithSidAttribute : Attribute {

        /// <summary>
        /// A list of unique identifiers for this Entity.
        /// </summary>
        public string[] IDs;

        /// <summary>
        /// Associate this entity with one or more IDs, without also registering a loader.<br/>
        /// Useful for internal entities that should be trackable by their SID, but not created via map data.
        /// </summary>
        /// <param name="ids">
        /// A list of unique identifiers for this Entity.
        /// </param>
        public AssociatedWithSidAttribute(params string[] ids) {
            IDs = ids;
        }
    }
}
