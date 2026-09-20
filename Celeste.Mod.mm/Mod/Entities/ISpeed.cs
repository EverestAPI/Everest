using System;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using MonoMod.InlineRT;

namespace Celeste.Mod {
    /// <summary>
    /// </summary>
    public interface ISpeed {
        /// <summary>
        /// </summary>
        public Vector2 Speed { get; set; }
    }
}

namespace MonoMod {

    /// <summary>
    /// Patch the given class to tack on the ISpeed interface
    /// </summary>
    [MonoModCustomAttribute(nameof(MonoModRules.PatchSpeedInterface))]
    class PatchSpeedInterfaceAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchSpeedInterface(ICustomAttributeProvider provider, CustomAttribute attrib) {
            InterfaceImplementation i_ISpeed = new InterfaceImplementation(MonoModRule.Modder.FindType("Celeste.Mod.ISpeed"));

            ((TypeDefinition) provider).Interfaces.Add(i_ISpeed);
        }

    }
}
