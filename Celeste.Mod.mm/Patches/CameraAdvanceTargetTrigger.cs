using Microsoft.Xna.Framework;
using Mono.Cecil;
using MonoMod;
using MonoMod.InlineRT;
using System;

namespace Celeste {
    [PatchCameraTargetTriggerInterface]
    class patch_CameraAdvanceTargetTrigger : CameraAdvanceTargetTrigger {
        public patch_CameraAdvanceTargetTrigger(EntityData data, Vector2 offset) : base(data, offset) { }

        [MonoModIgnore]
        [PatchInterface]
        public extern override void OnStay(Player player);
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the Camera Target Trigger to implement <see cref="Celeste.Mod.Entities.ICameraTargetTrigger"/>.
    /// </summary>
    [MonoModCustomAttribute(nameof(MonoModRules.PatchCameraTargetTriggerInterface))]
    class PatchCameraTargetTriggerInterfaceAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchCameraTargetTriggerInterface(ICustomAttributeProvider provider, CustomAttribute attrib) {
            InterfaceImplementation i_cameraTargetTrigger = new(MonoModRule.Modder.FindType("Celeste.Mod.Entities.ICameraTargetTrigger"));

            ((TypeDefinition) provider).Interfaces.Add(i_cameraTargetTrigger);
        }
    }
}
