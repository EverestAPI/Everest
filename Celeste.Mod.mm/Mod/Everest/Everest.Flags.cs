using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Celeste.Mod {
    public static partial class Everest {
        public static class Flags {
            /// <summary>
            /// Is the game running on XNA - always false on .NET Core Everest.
            /// </summary>
            [Obsolete("`IsXNA` is always false on Everest Core")]
            public static bool IsXNA => false;

            /// <summary>
            /// Is the game running on FNA - always true on .NET Core Everest.
            /// </summary>
            [Obsolete("`IsFNA` is always true on Everest Core")]
            public static bool IsFNA => true;

            /// <summary>
            /// Is the vanilla install running on XNA?
            /// </summary>
            public static bool VanillaIsXNA { get; private set; }

            /// <summary>
            /// Is the vanilla install running on FNA?
            /// </summary>
            public static bool VanillaIsFNA { get; private set; }

            /// <summary>
            /// Is Everest running without a window?
            /// </summary>
            public static bool IsHeadless { get; internal set; }

            /// <summary>
            /// Is the game running using Mono - always false on .NET Core Everest.
            /// </summary>
            [Obsolete("`IsMono` is always false on Everest Core")]
            public static bool IsMono => false;

            /// <summary>
            /// Should the game avoid creating render targets if possible?
            /// </summary>
            public static bool AvoidRenderTargets { get; private set; }
            /// <summary>
            /// Does the environment (platform, ...) prefer lazy loading?
            /// Used to be used by android, which is not supported currently.
            /// </summary>
            [Obsolete("`PreferLazyLoading` is always false on Everest Core")]
            public static bool PreferLazyLoading => false;

            /// <summary>
            /// Does the environment (renderer, framework ,...) prefer threaded GL?
            /// </summary>
            [Obsolete("`PreferThreadedGL` is always false on Everest Core")]
            public static bool PreferThreadedGL => false;

            /// <summary>
            /// Does the environment (platform, ...) support loading runtime mods?
            /// </summary>
            public static bool SupportRuntimeMods { get; private set; }

            /// <summary>
            /// Does the environment (platform, ...) support updating Everest?
            /// </summary>
            public static bool SupportUpdatingEverest { get; private set; }

            internal static void Initialize() {
                // Determine vanilla install type
                string vanillaExe = Path.Combine(PathGame, "orig", "Celeste.exe");
                if (File.Exists(vanillaExe)) {
                    using FileStream stream = File.OpenRead(vanillaExe);
                    using PEReader peReader = new PEReader(stream);
                    MetadataReader metaReader = peReader.GetMetadataReader();

                    VanillaIsFNA = metaReader.AssemblyReferences.Any(handle => metaReader.GetString(metaReader.GetAssemblyReference(handle).Name) == "FNA");
                    VanillaIsXNA = !VanillaIsFNA;
                }

                AvoidRenderTargets = Environment.GetEnvironmentVariable("EVEREST_NO_RT") == "1";

                SupportRuntimeMods = true;
                SupportUpdatingEverest = true;
            }

        }
    }
}
