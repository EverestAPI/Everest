using System;

namespace Celeste.Mod.UI {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class OuiPropertiesAttribute : Attribute {
        /// <summary>
        /// Whether the mountain music for the current map should play in this menu.
        /// </summary>
        public bool PlayCustomMusic { get; }

        /// <summary>
        /// Configures extra properties for this Oui.
        /// </summary>
        /// <param name="playCustomMusic">A list of unique identifiers for this Backdrop.</param>
        public OuiPropertiesAttribute(bool playCustomMusic = false) {
            PlayCustomMusic = playCustomMusic;
        }
    }
}
