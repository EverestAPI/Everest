#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Monocle {
    static class patch_VirtualContent {

        // We're effectively in VirtualContent, but still need to "expose" private fields to our mod.
        private static List<VirtualAsset> assets;
        private static bool reloading;

        // Allow loading VirtualTextures from modded AssetMetadatas.

        public static VirtualTexture CreateTexture(AssetMetadata metadata) {
            VirtualTexture virtualTexture = (VirtualTexture) (object) new patch_VirtualTexture(metadata);
            assets.Add(virtualTexture);
            return virtualTexture;
        }

        [MonoModIgnore]
        internal static extern void Reload();
        public static void _Reload()
            => Reload();
        
        public static void ForceReload() {
            reloading = true;
            Reload();
        }

    }
    public static class VirtualContentExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static VirtualTexture CreateTexture(AssetMetadata metadata)
            => patch_VirtualContent.CreateTexture(metadata);

        public static void Reload()
            => patch_VirtualContent._Reload();

        public static void ForceReload()
            => patch_VirtualContent.ForceReload();

    }
}
