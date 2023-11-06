using MonoMod;
using System;

namespace Monocle {
    // This is only required as VirtualAsset's members are internal or even private, not protected.
    // Noel or Matt, if you see this, please change the visibility to protected. Thanks!
    [MonoModIgnore]
    abstract class patch_VirtualAsset : VirtualAsset {

#pragma warning disable CS0108
        public string Name { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
# pragma warning restore CS0108

        internal virtual void Unload() {
        }

        internal virtual void Reload() {
        }

    }
    public static class VirtualAssetExt {

        /// <summary>
        /// Unloads a virtual asset without removing it from the virtual asset list.
        /// </summary>
        /// <param name="self">The asset to unload.</param>
        [Obsolete("Use VirtualAsset.Unload instead.")]
        public static void Unload(this VirtualAsset self)
            => ((patch_VirtualAsset) (object) self).Unload();

        /// <summary>
        /// Reloads a single virtual asset.
        /// </summary>
        /// <param name="self">The asset to reload.</param>
        [Obsolete("Use VirtualAsset.Reload instead.")]
        public static void Reload(this VirtualAsset self)
            => ((patch_VirtualAsset) (object) self).Reload();

    }
}
