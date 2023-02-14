using Microsoft.Xna.Framework;
using System;

namespace Celeste.Mod {
    public static partial class Everest {
        public static class Flags {
            /// <summary>
            /// Is the game running on XNA?
            /// </summary>
            public static bool IsXNA { get; private set; }

            /// <summary>
            /// Is the game running on FNA?
            /// </summary>
            public static bool IsFNA { get; private set; }

            /// <summary>
            /// Is Everest running headlessly?
            /// </summary>
            public static bool IsHeadless { get; private set; }

            /// <summary>
            /// Is the game running using Mono?
            /// </summary>
            public static bool IsMono { get; private set; }

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
            /// Does the environment (platform, ...) support updating Everest?
            /// </summary>
            public static bool SupportUpdatingEverest { get; private set; }

            internal static void Initialize() {
                IsFNA = typeof(Game).Assembly.FullName.Contains("FNA");
                IsXNA = !IsFNA;

                IsHeadless = Environment.GetEnvironmentVariable("EVEREST_HEADLESS") == "1";

                IsMono = Type.GetType("Mono.Runtime") != null;

                AvoidRenderTargets = Environment.GetEnvironmentVariable("EVEREST_NO_RT") == "1";
                PreferLazyLoading = false;

                // The way how FNA3D's D3D11 implementation handles threaded GL is hated by a few drivers.
                PreferThreadedGL = IsXNA;

                SupportRuntimeMods = true;
                SupportUpdatingEverest = true;
            }

        }
    }
}
