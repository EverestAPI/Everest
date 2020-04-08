#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Core;
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

        [MonoModLinkFrom("Microsoft.Xna.Framework.Graphics.Texture2D Monocle.VirtualTexture::Texture_Unsafe")]
        public Texture2D Texture;

        [MonoModRemove]
        public Texture2D Texture_Unsafe;

        private bool _Texture_Reloading;
        [MonoModLinkFrom("Microsoft.Xna.Framework.Graphics.Texture2D Monocle.VirtualTexture::Texture")]
        public Texture2D Texture_Safe {
            get {
                if (_Texture_Reloading || !CoreModule.Settings.LazyLoading)
                    return Texture_Unsafe;

                // If we're accessing the texture from outside (render), load lazily if required.
                if (Texture_Unsafe?.IsDisposed ?? true)
                    Reload();

                return Texture_Unsafe;
            }
            set {
                Texture_Unsafe = value;
            }
        }

        public ModAsset Metadata;

        public VirtualTexture Fallback;

        [MonoModConstructor]
        [MonoModReplace]
        internal patch_VirtualTexture(string path) {
            Path = path;
            Name = path;
            Preload();
        }

        [MonoModConstructor]
        [MonoModReplace]
        internal patch_VirtualTexture(string name, int width, int height, Color color) {
            Name = name;
            Width = width;
            Height = height;
            this.color = color;
            Preload();
        }

        [MonoModConstructor]
        internal patch_VirtualTexture(ModAsset metadata) {
            Metadata = metadata;
            Name = metadata.PathVirtual;
            Preload();
        }

        internal extern void orig_Unload();
        internal override void Unload() {
            _Texture_Reloading = true;
            orig_Unload();
            _Texture_Reloading = false;
        }

        internal extern void orig_Reload();
        internal override void Reload() {
            _Texture_Reloading = true;

            if (Metadata == null) {
                orig_Reload();
                _Texture_Reloading = false;
                return;
            }

            Unload();
            Texture = null;

            Stream stream = Metadata.Stream;
            if (stream != null) {
                bool premul = false; // Assume unpremultiplied by default.
                if (Metadata.TryGetMeta(out TextureMeta meta))
                    premul = meta.Premultiplied;

                using (stream) {
                    if (premul) {
                        Texture = MainThreadHelper.Get(() => Texture2D.FromStream(Celeste.Celeste.Instance.GraphicsDevice, stream)).GetResult();
                    } else {
                        ContentExtensions.LoadTextureLazyPremultiply(Celeste.Celeste.Instance.GraphicsDevice, stream, out int w, out int h, out byte[] data);
                        Texture = MainThreadHelper.Get(() => {
                            Texture2D tex = new Texture2D(Celeste.Celeste.Instance.GraphicsDevice, w, h, false, SurfaceFormat.Color);
                            tex.SetData(data);
                            return tex;
                        }).GetResult();
                    }
                }

            } else if (Fallback != null) {
                ((patch_VirtualTexture) (object) Fallback).Reload();
                Texture = Fallback.Texture;
            }

            if (Texture != null) {
                Width = Texture.Width;
                Height = Texture.Height;
            }

            _Texture_Reloading = false;
        }

        private void Preload() {
            if (!CoreModule.Settings.LazyLoading) {
                Reload();
                return;
            }

            // Preload the width / height, and if needed, the entire texture.

            if (!string.IsNullOrEmpty(Path)) {
                string extension = System.IO.Path.GetExtension(Path);
                if (extension == ".data") {
                    // Easy.
                    using (FileStream stream = File.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
                    using (BinaryReader reader = new BinaryReader(stream)) {
                        Width = reader.ReadInt32();
                        Height = reader.ReadInt32();
                    }

                } else if (extension == ".png") {
                    // Hard.
                    using (FileStream stream = File.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
                        GetSizeFromPNG(stream);

                } else {
                    // .xnb and other file formats - impossible.
                    Reload();

                }

            } else if (Metadata != null) {
                if (Metadata.Format == "png") {
                    // Hard.
                    using (Stream stream = Metadata.Stream)
                        GetSizeFromPNG(stream);

                } else {
                    // .xnb and other file formats - impossible.
                    Reload();
                }
            }
        }

        private void GetSizeFromPNG(Stream stream) {
            using (BinaryReader reader = new BinaryReader(stream)) {
                ulong magic = reader.ReadUInt64();
                if (magic != 0x0A1A0A0D474E5089U) {
                    Celeste.Mod.Logger.Log(LogLevel.Error, "vtex", $"Failed preloading PNG: Expected 0x0A1A0A0D474E5089, got 0x{magic.ToString("X16")} - {Path}");
                    throw new InvalidDataException("PNG magic mismatch!");
                }
                uint length = reader.ReadUInt32();
                if (length != 0x0D000000U) {
                    Celeste.Mod.Logger.Log(LogLevel.Error, "vtex", $"Failed preloading PNG: Expected 0x0D000000, got 0x{length.ToString("X8")} - {Path}");
                    throw new InvalidDataException("First chunk of PNG not 0x0000000D (13) bytes long!");
                }
                uint chunk = reader.ReadUInt32();
                if (chunk != 0x52444849U) {
                    Celeste.Mod.Logger.Log(LogLevel.Error, "vtex", $"Failed preloading PNG: Expected 0x52444849, got 0x{chunk.ToString("X8")} - {Path}");
                    throw new InvalidDataException("PNG doesn't start with IHDR!");
                }
                Width = SwapEndian(reader.ReadInt32());
                Height = SwapEndian(reader.ReadInt32());
            }
        }

        private static int SwapEndian(int data) {
            return
                ((data & 0xFF) << 24) |
                (((data >> 8) & 0xFF) << 16) |
                (((data >> 16) & 0xFF) << 8) |
                ((data >> 24) & 0xFF);
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
