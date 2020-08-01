#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0414 // The field is assigned but its value is never used

using Celeste.Mod;
using Microsoft.Xna.Framework.Graphics;
using MonoMod;
using System.Collections.Generic;
using System.IO;

namespace Monocle {
    static class patch_VirtualContent {

        // We're effectively in VirtualContent, but still need to "expose" private fields to our mod.
        private static List<VirtualAsset> assets;
        public static List<VirtualAsset> Assets => assets;
        private static bool reloading;

        // Allow loading VirtualTextures from modded AssetMetadatas.

        [MonoModReplace]
        public static VirtualTexture CreateTexture(string path) {
            VirtualTexture vt;

            // Trim the file extension, as we don't store it in our mod content mappings.
            string dir = Path.GetDirectoryName(path);
            string pathMod = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrEmpty(dir))
                pathMod = Path.Combine(dir, pathMod);
            // We use / instead of \ in mod content paths.
            pathMod = pathMod.Replace('\\', '/');

            if (Everest.Content.TryGet<Texture2D>(pathMod, out ModAsset asset)) {
                vt = (VirtualTexture) (object) new patch_VirtualTexture(asset);
            } else {
                vt = (VirtualTexture) (object) new patch_VirtualTexture(path);
            }

            assets.Add(vt);
            return vt;
        }

        public static VirtualTexture CreateTexture(ModAsset metadata) {
            VirtualTexture virtualTexture = (VirtualTexture) (object) new patch_VirtualTexture(metadata);
            assets.Add(virtualTexture);
            return virtualTexture;
        }

        [MonoModIgnore]
        internal static extern void Reload();
        public static void _Reload()
            => Reload();

        [MonoModIgnore]
        internal static extern void Unload();
        public static void _Unload()
            => Unload();

        public static void ForceReload() {
            reloading = true;
            Reload();
        }

        public static void UnloadOverworld() {
            foreach (VirtualAsset asset in assets) {
                string path = asset.Name.Replace('\\', '/');
                if (asset is VirtualTexture && path.StartsWith("Graphics/Atlases/")) {
                    path = path.Substring(17);
                    if (path.StartsWith("Opening") || path.StartsWith("Overworld") || path.StartsWith("Mountain") || path.StartsWith("Journal")) {
                        asset.Unload();
                    }
                }
            }
        }

    }
    public static class VirtualContentExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// The list of all managed VirtualAssets.
        /// </summary>
        public static List<VirtualAsset> Assets => patch_VirtualContent.Assets;

        /// <summary>
        /// Create a new VirtualTexture based on the passed mod asset.
        /// </summary>
        public static VirtualTexture CreateTexture(ModAsset metadata)
            => patch_VirtualContent.CreateTexture(metadata);

        /// <summary>
        /// Reload all content.
        /// </summary>
        public static void Reload()
            => patch_VirtualContent._Reload();

        /// <summary>
        /// Unload all content.
        /// </summary>
        public static void Unload()
            => patch_VirtualContent._Unload();

        /// <summary>
        /// Forcibly unload and reload all content.
        /// </summary>
        public static void ForceReload()
            => patch_VirtualContent.ForceReload();

        /// <summary>
        /// Unload all overworld-related content.
        /// </summary>
        public static void UnloadOverworld()
            => patch_VirtualContent.UnloadOverworld();

    }
}
