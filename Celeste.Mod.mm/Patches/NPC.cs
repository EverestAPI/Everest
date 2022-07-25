#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste {
    class patch_NPC : NPC {

        public patch_NPC(Vector2 position) : base(position) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModIgnore]
        [PatchNPCSetupGrannySpriteSounds]
        public new extern void SetupGrannySpriteSounds();

        [MonoModIgnore]
        [PatchNPCSetupTheoSpriteSounds]
        public new extern void SetupTheoSpriteSounds();

    }
}

namespace MonoMod {
    /// <summary>
    /// Patches the method to un-hardcode the FMOD event string used to play the footstep sound effect.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchNPCSetupGrannySpriteSounds))]
    class PatchNPCSetupGrannySpriteSoundsAttribute : Attribute { }

    /// <summary>
    /// Patches the method to un-hardcode the FMOD event string used to play the footstep sound effect.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchNPCSetupTheoSpriteSounds))]
    class PatchNPCSetupTheoSpriteSoundsAttribute : Attribute { }

    static partial class MonoModRules {

        private static void PatchNPCSetupSpriteSoundsOnFrameChange(ILContext context) {
            PatchPlaySurfaceIndex(new ILCursor(context), "/footstep");
        }

        public static void PatchNPCSetupGrannySpriteSounds(MethodDefinition method, CustomAttribute attrib) {
            method = method.DeclaringType.FindMethod("<SetupGrannySpriteSounds>b__20_0");

            new ILContext(method).Invoke(PatchNPCSetupSpriteSoundsOnFrameChange);
        }

        public static void PatchNPCSetupTheoSpriteSounds(MethodDefinition method, CustomAttribute attrib) {
            method = method.DeclaringType.FindMethod("<SetupTheoSpriteSounds>b__19_0");

            new ILContext(method).Invoke(PatchNPCSetupSpriteSoundsOnFrameChange);
        }

    }
}
