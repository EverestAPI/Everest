using MonoMod;
using System;

namespace Monocle {
    abstract class patch_VirtualAsset : VirtualAsset {

#pragma warning disable CS0108
        [MonoModIgnore]
        public string Name { get; internal set; }
        
        // Making Width and Height virtual is a breaking change, so lets just add new virtual properties and make the
        // old ones just wrap the new ones :)
        protected virtual int InnerWidth { get; set; }
        public int Width {
            [MonoModReplace]
            get => InnerWidth;
            [MonoModReplace]
            internal set => InnerWidth = value;
        }
        
        protected virtual int InnerHeight { get; set; }
        public int Height { 
            [MonoModReplace]
            get => InnerHeight;
            [MonoModReplace]
            internal set => InnerHeight = value;
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
