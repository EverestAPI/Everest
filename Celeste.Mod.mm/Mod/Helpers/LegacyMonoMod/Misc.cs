using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using System;
using System.Runtime.InteropServices;

namespace Celeste.Mod.Helpers.LegacyMonoMod {
    public static class ILShims {
        [RelinkLegacyMonoMod("Mono.Cecil.Cil.Instruction MonoMod.Cil.ILLabel::Target")]
        public static Instruction ILLabel_GetTarget(ILLabel label) => label.Target; // This previously used to be a field
    }

    [RelinkLegacyMonoMod("MonoMod.Utils.PlatformHelper")]
    public static class LegacyPlatformHelper {

        [Flags]
        [RelinkLegacyMonoMod("MonoMod.Utils.Platform")]
        public enum Platform : int {
            OS = 1 << 0,
            Bits64 = 1 << 1,
            NT = 1 << 2,
            Unix = 1 << 3,
            ARM = 1 << 16,
            Wine = 1 << 17,
            Unknown = OS | (1 << 4),
            Windows = OS | NT | (1 << 5),
            MacOS = OS | Unix | (1 << 6),
            Linux = OS | Unix | (1 << 7),
            Android = Linux | (1 << 8),
            iOS = MacOS | (1 << 9),
        }

        private static Platform? _current;
        public static Platform Current {
            get => _current ??=
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Platform.Windows :
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Platform.MacOS :
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? Platform.Linux :
                    Platform.Unknown;
            set => throw new NotSupportedException("PlatformHelper.set_Current is no longer supported");
        }

        public static bool Is(Platform platform) => (Current & platform) == platform;

        private static string _librarySuffix;
        public static string LibrarySuffix => _librarySuffix ??= Is(Platform.MacOS) ? "dylib" : Is(Platform.Unix) ? "so" : "dll";

    }
}