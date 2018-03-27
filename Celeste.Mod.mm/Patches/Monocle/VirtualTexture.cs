#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Helpers;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    class patch_VirtualTexture : patch_VirtualAsset {

        // We're effectively in VirtualAsset, but still need to "expose" private fields to our mod.
        public string Path { get; private set; }
        private Color color;

        [MonoModHook("Microsoft.Xna.Framework.Graphics.Texture2D Monocle.VirtualTexture::Texture_Unsafe")]
        public Texture2D Texture;

        [MonoModRemove]
        public Texture2D Texture_Unsafe;

        private bool _Texture_ForceUnsafe;
        [MonoModHook("Microsoft.Xna.Framework.Graphics.Texture2D Monocle.VirtualTexture::Texture")]
        public Texture2D Texture_Safe {
            get {
                // If we're "manipulating" the texture (unload or reload), directly pass on to the field.
                if (_Texture_ForceUnsafe)
                    return Texture_Unsafe;

                // If we're accessing the texture from outside (render), load lazily if required.
                if (Texture_Unsafe?.IsDisposed ?? true) {
                    Reload();
                }

                return Texture_Unsafe;
            }
            set {
                Texture_Unsafe = value;
            }
        }

        public ModAsset Metadata { get; private set; }

        public VirtualTexture Fallback;

        [MonoModConstructor]
        internal patch_VirtualTexture(string path) {
            Path = path;
            Name = path;
            // Reload();
        }

        [MonoModConstructor]
        internal patch_VirtualTexture(string name, int width, int height, Color color) {
            Name = name;
            Width = width;
            Height = height;
            this.color = color;
            // Reload();
        }

        [MonoModConstructor]
        internal patch_VirtualTexture(ModAsset metadata) {
            Metadata = metadata;
            Name = metadata.PathMapped;
            // Reload();
        }

        internal extern void orig_Reload();
        internal override void Reload() {
            _Texture_ForceUnsafe = true;

            if (Metadata == null) {
                orig_Reload();
                _Texture_ForceUnsafe = false;
                return;
            }

            Unload();
            Texture = null;

            Stream stream = Metadata.Stream;
            if (stream != null) {
                using (stream)
                    Texture = Texture2D.FromStream(Celeste.Celeste.Instance.GraphicsDevice, stream);

                TextureMeta meta;
                if (Metadata.TryGetMeta(out meta)) {

                    if (!meta.Premultiplied)
                        Texture.Premultiply();

                } else {
                    // Assume unpremultiplied by default.
                    Texture.Premultiply();
                }

            } else if (Fallback != null) {
                ((patch_VirtualTexture) (object) Fallback).Reload();
                Texture = Fallback.Texture;
            }

            if (Texture != null) {
                Width = Texture.Width;
                Height = Texture.Height;
            }

            _Texture_ForceUnsafe = false;
        }

    }
    public static class VirtualTextureExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// If the VirtualTexture originates from a mod, get the mod asset metadata.
        /// </summary>
        public static ModAsset GetMetadata(this VirtualTexture self)
            => ((patch_VirtualTexture) (object) self).Metadata;

        /// <summary>
        /// Set a fallback texture in case the texture becomes unavailable on reload.
        /// </summary>
        public static void SetFallback(this VirtualTexture self, VirtualTexture fallback)
            => ((patch_VirtualTexture) (object) self).Fallback = fallback;

    }
}
