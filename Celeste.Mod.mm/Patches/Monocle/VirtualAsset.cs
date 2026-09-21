using MonoMod;
using System;

namespace Monocle {
    abstract class patch_VirtualAsset : VirtualAsset {

#pragma warning disable CS0108
        [MonoModIgnore]
        public string Name { get; internal set; }
        
        protected int _width;
        public int Width {
            [MonoModReplace]
            get => _width;
            [MonoModReplace]
            internal set {
                if (_width != value) {
                    _width = value;
                    HandleSizeChange();
                }
            }
        }

        protected int _height;
        public int Height {
            [MonoModReplace]
            get => _height;
            [MonoModReplace]
            internal set {
                if (_height != value) {
                    _height = value;
                    HandleSizeChange();
                }
            }
        }

        protected virtual void HandleSizeChange() {
        }
        
# pragma warning restore CS0108
        // This is only required as VirtualAsset's members are internal or even private, not protected.
        // Noel or Maddy, if you see this, please change the visibility to protected. Thanks!
        [MonoModIgnore]
        internal virtual void Unload() {
        }

        [MonoModIgnore]
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
