using System;

namespace Celeste.Mod.Backdrops {
    /// <summary>
    /// Marks this backdrop as a Custom <see cref="Backdrop"/>.
    /// <br></br>
    /// This Backdrop will be loaded when a matching ID is detected.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class CustomBackdropAttribute : Attribute {
        /// <summary>
        /// A list of unique identifiers for this Backdrop.
        /// </summary>
        public string[] IDs { get; }

        /// <summary>
        /// Marks this backdrop as a Custom <see cref="Backdrop"/>.
        /// </summary>
        /// <param name="ids">A list of unique identifiers for this Backdrop.</param>
        public CustomBackdropAttribute(params string[] ids) {
            IDs = ids;
        }
    }
}
