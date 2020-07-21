using System;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// Adds this <see cref="Strawberry"/> or <see cref="IStrawberry"/> to the <see cref="StrawberryRegistry"/>,
    /// <br></br>
    /// and allows it to be taken into account correctly in the total strawberry count.
    /// <br></br>
    /// <see href="https://github.com/EverestAPI/Resources/wiki/Custom-Entities-and-Triggers#registerstrawberry">Read More.</see>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class RegisterStrawberryAttribute : Attribute {

        /// <summary>
        /// Whether the berry should be counted in the maximum berry count. 
        /// </summary>
        public bool isTracked;

        /// <summary>
        /// Whether the berry has specific collection rules.
        /// </summary>
        public bool blocksNormalCollection;

        /// <summary>
        /// Adds this <see cref="Strawberry"/> or <see cref="IStrawberry"/> to the <see cref="StrawberryRegistry"/>.
        /// </summary>
        /// <param name="tracked">Whether the berry should be counted in the maximum berry count. </param>
        /// <param name="blocksCollection">Whether the berry has specific collection rules.</param>
        public RegisterStrawberryAttribute(bool tracked, bool blocksCollection) {
            isTracked = tracked;
            blocksNormalCollection = blocksCollection;
        }
    }
}
