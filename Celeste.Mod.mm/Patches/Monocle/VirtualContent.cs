#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0414 // The field is assigned but its value is never used

using Celeste.Mod;
using Microsoft.Xna.Framework.Graphics;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;

namespace Monocle {
    static class patch_VirtualContent {

        // We're effectively in VirtualContent, but still need to "expose" private fields to our mod.
        private static List<VirtualAsset> assets;
        /// <summary>
        /// The list of all managed VirtualAssets.
        /// </summary>
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

        /// <summary>
        /// Create a new VirtualTexture based on the passed mod asset.
        /// </summary>
        public static VirtualTexture CreateTexture(ModAsset metadata) {
            VirtualTexture virtualTexture = (VirtualTexture) (object) new patch_VirtualTexture(metadata);
            assets.Add(virtualTexture);
            return virtualTexture;
        }

        [MonoModIgnore]
        [MonoModPublic]
        [MonoModLinkFrom("System.Void Monocle.VirtualContent::_Reload()")]
        public static extern void Reload();

        [MonoModIgnore]
        [MonoModPublic]
        [MonoModLinkFrom("System.Void Monocle.VirtualContent::_Unload()")]
        public static extern void Unload();

        /// <summary>
        /// Forcibly unload and reload all content.
        /// </summary>
        public static void ForceReload() {
            reloading = true;
            Reload();
        }

        /// <summary>
        /// Unload all overworld-related content.
        /// </summary>
        public static void UnloadOverworld() {
            foreach (patch_VirtualAsset asset in assets) {
                string path = asset.Name.Replace('\\', '/');
                if (asset is patch_VirtualTexture && path.StartsWith("Graphics/Atlases/")) {
                    path = path.Substring(17);
                    if (path.StartsWith("Opening") || path.StartsWith("Overworld") || path.StartsWith("Mountain") || path.StartsWith("Journal")) {
                        asset.Unload();
                    }
                }
            }
        }

    }
    public static class VirtualContentExt {

        /// <inheritdoc cref="patch_VirtualContent.Assets"/>
        [Obsolete("Use VirtualContent.Assets instead.")]
        public static List<VirtualAsset> Assets => patch_VirtualContent.Assets;

        /// <inheritdoc cref="patch_VirtualContent.CreateTexture(ModAsset)"/>
        [Obsolete("Use VirtualContent.CreateTexture instead.")]
        public static VirtualTexture CreateTexture(ModAsset metadata)
            => patch_VirtualContent.CreateTexture(metadata);

        /// <summary>
        /// Reload all content.
        /// </summary>
        [Obsolete("Use VirtualContent.Reload instead.")]
        public static void Reload()
            => patch_VirtualContent.Reload();

        /// <summary>
        /// Unload all content.
        /// </summary>
        [Obsolete("Use VirtualContent.Unload instead.")]
        public static void Unload()
            => patch_VirtualContent.Unload();

        /// <inheritdoc cref="patch_VirtualContent.ForceReload"/>
        [Obsolete("Use VirtualContent.ForceReload instead.")]
        public static void ForceReload()
            => patch_VirtualContent.ForceReload();

        /// <inheritdoc cref="patch_VirtualContent.UnloadOverworld"/>
        [Obsolete("Use VirtualContent.UnloadOverworld instead.")]
        public static void UnloadOverworld()
            => patch_VirtualContent.UnloadOverworld();

    }
}
