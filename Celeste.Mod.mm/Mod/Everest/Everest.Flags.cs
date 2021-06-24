using Microsoft.Xna.Framework;
using System;

namespace Celeste.Mod {
    public static partial class Everest {
        public static class Flags {

            /// <summary>
            /// Is Everest running headlessly?
            /// </summary>
            public static bool IsHeadless { get; private set; }

            /// <summary>
            /// Is the game running using Mono?
            /// </summary>
            public static bool IsMono { get; private set; }

            /// <summary>
            /// Is the game running on a mobile platform, f.e. Android?
            /// </summary>
            public static bool IsMobile { get; private set; }

            /// <summary>
            /// Is the game running on Android?
            /// </summary>
            public static bool IsAndroid { get; private set; }
            /// <summary>
            /// Is the game running using FNADroid?
            /// </summary>
            public static bool IsFNADroid { get; private set; }

            /// <summary>
            /// Should the game avoid creating render targets if possible?
            /// </summary>
            public static bool AvoidRenderTargets { get; private set; }
            /// <summary>
            /// Does the environment (platform, ...) prefer lazy loading?
            /// </summary>
            public static bool PreferLazyLoading { get; private set; }

            /// <summary>
            /// Does the environment (renderer, framework ,...) prefer threaded GL?
            /// </summary>
            public static bool PreferThreadedGL { get; private set; }

            /// <summary>
            /// Does the environment (platform, ...) support loading runtime mods?
            /// </summary>
            public static bool SupportRuntimeMods { get; private set; }
            /// <summary>
            /// Does the environment (platform, ...) support relinking runtime mods?
            /// </summary>
            public static bool SupportRelinkingMods { get; private set; }
            /// <summary>
            /// Does the environment (platform, ...) support updating Everest?
            /// </summary>
            public static bool SupportUpdatingEverest { get; private set; }

            internal static void Initialize() {
                IsHeadless = Environment.GetEnvironmentVariable("EVEREST_HEADLESS") == "1";

                IsMono = Type.GetType("Mono.Runtime") != null;

                IsFNADroid = Environment.GetEnvironmentVariable("FNADROID") == "1";
                IsAndroid = IsFNADroid;

                IsMobile = IsAndroid;

                AvoidRenderTargets = IsMobile || Environment.GetEnvironmentVariable("EVEREST_NO_RT") == "1";
                PreferLazyLoading = IsMobile;

                // The way how FNA3D's D3D11 implementation handles threaded GL is hated by a few drivers.
                PreferThreadedGL = !typeof(Game).Assembly.FullName.Contains("FNA");

                SupportRuntimeMods = true;
                SupportRelinkingMods = !IsMobile; // FIXME: Mono.Cecil can't find GAC when using Xamarin.*
                SupportUpdatingEverest = !IsMobile; // FIXME: Mono.Cecil can't find GAC when using Xamarin.*
            }

        }
    }
}
