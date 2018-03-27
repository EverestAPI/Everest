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

        private bool _Texture_Reloading;
        [MonoModHook("Microsoft.Xna.Framework.Graphics.Texture2D Monocle.VirtualTexture::Texture")]
        public Texture2D Texture_Safe {
            get {
                // If we're reloading, directly pass on to the field.
                if (_Texture_Reloading)
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

        public ModAsset Metadata { get; private set; }

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
            Name = metadata.PathMapped;
            Preload();
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

            _Texture_Reloading = false;
        }

        private void Preload() {
            // Preload any important data, preferably metadata only.

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

                } else if (extension == ".xnb") {
                    // Impossible.
                    Texture = Engine.Instance.Content.Load<Texture2D>(Path.Replace(".xnb", ""));
                    Width = Texture.Width;
                    Height = Texture.Height;

                } else {
                    // FFFUU~
                    using (FileStream stream = File.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
                        Texture = Texture2D.FromStream(Engine.Graphics.GraphicsDevice, stream);
                    Width = Texture.Width;
                    Height = Texture.Height;
                }

            } else if (Metadata != null) {
                if (Metadata.AssetFormat == "png") {
                    // Hard.
                    using (Stream stream = Metadata.Stream)
                        GetSizeFromPNG(stream);

                } else {
                    // FFFUU~
                    using (Stream stream = Metadata.Stream)
                        Texture = Texture2D.FromStream(Engine.Graphics.GraphicsDevice, stream);
                    Width = Texture.Width;
                    Height = Texture.Height;
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
                Width = reader.ReadInt32();
                Height = reader.ReadInt32();
            }
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
