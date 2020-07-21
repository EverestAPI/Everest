#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;

namespace Monocle {
    // This is only required as VirtualAsset's members are internal or even private, not protected.
    // Noel or Matt, if you see this, please change the visibility to protected. Thanks!
    [MonoModIgnore]
    abstract class patch_VirtualAsset {

        public string Name { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }

        internal virtual void Unload() {
        }

        internal virtual void Reload() {
        }

    }
    public static class VirtualAssetExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// Unloads a virtual asset without removing it from the virtual asset list.
        /// </summary>
        /// <param name="self">The asset to unload.</param>
        public static void Unload(this VirtualAsset self)
            => ((patch_VirtualAsset) (object) self).Unload();

        /// <summary>
        /// Reloads a single virtual asset.
        /// </summary>
        /// <param name="self">The asset to reload.</param>
        public static void Reload(this VirtualAsset self)
            => ((patch_VirtualAsset) (object) self).Reload();

    }
}
