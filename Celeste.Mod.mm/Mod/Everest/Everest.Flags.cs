using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Monocle;
using MonoMod;
using MonoMod.Helpers;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static partial class Everest {
        public static class Flags {

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
            /// Does the environment (platform, ...) prefer LQ texture atlases?
            /// </summary>
            public static bool PreferLQAtlas { get; private set; }

            /// <summary>
            /// Does the environment (platform, ...) support loading runtime mods?
            /// </summary>
            public static bool SupportRuntimeMods { get; private set; }
            /// <summary>
            /// Does the environment (platform, ...) support relinking runtime mods?
            /// </summary>
            public static bool SupportRelinkingMods { get; private set; }

            internal static void Initialize() {
                IsMono = Type.GetType("Mono.Runtime") != null;

                IsFNADroid = Environment.GetEnvironmentVariable("FNADROID") == "1";
                IsAndroid = IsFNADroid;

                IsMobile = IsAndroid;

                AvoidRenderTargets = IsMobile;
                PreferLazyLoading = IsMobile;
                PreferLQAtlas = IsMobile;

                SupportRuntimeMods = true;
                SupportRelinkingMods = !IsMobile;
            }

        }
    }
}
