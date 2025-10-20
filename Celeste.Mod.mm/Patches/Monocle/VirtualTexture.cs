#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

#nullable enable

namespace Monocle {
    class patch_VirtualTexture : patch_VirtualAsset {

        // We're effectively in VirtualAsset, but still need to "expose" private fields to our mod.
        public string? Path { get; private set; }
        private Color color;

        internal const int bytesSize = 512 * 1024; // 524288
        internal const int bytesCheckSize = 512 * 1024 - 32; // 524256
        
        private static extern void orig_cctor();
        [MonoModConstructor]
        private static void cctor() {
            orig_cctor();
        }

        // The following maps all refs Texture_Unsafe to Texture
        // and maps all vanilla Texture refs to Texture_Safe
        // Texture_Unsafe gets erased after

        [MonoModLinkFrom("Microsoft.Xna.Framework.Graphics.Texture2D Monocle.VirtualTexture::Texture_Unsafe")]
        public Texture2D? Texture;

        [MonoModRemove] 
        private Texture2D Texture_Unsafe;

        [MonoModLinkFrom("Microsoft.Xna.Framework.Graphics.Texture2D Monocle.VirtualTexture::Texture")]
        public Texture2D Texture_Safe {
            get {
                return Texture_Unsafe;
            }
            set {
                Texture_Unsafe = value;
            }
        }

        public ModAsset? Metadata;

        public VirtualTexture? Fallback;

        [MonoModConstructor]
        [MonoModReplace]
        internal patch_VirtualTexture(string path) {
            Path = path;
            Name = path;
            // if (!Preload(force: Everest.Flags.IsHeadless) && !Everest.Flags.IsHeadless)
                Reload();
            if (Everest.Flags.IsHeadless)
                Texture_Unsafe = new Texture2D(Engine.Graphics.GraphicsDevice, Width, Height);
        }

        [MonoModConstructor]
        [MonoModReplace]
        internal patch_VirtualTexture(string name, int width, int height, Color color) {
            Name = name;
            Width = width;
            Height = height;
            this.color = color;
            // if (!Preload(force: Everest.Flags.IsHeadless) && !Everest.Flags.IsHeadless)
                Reload();
            if (Everest.Flags.IsHeadless)
                Texture_Unsafe = new Texture2D(Engine.Graphics.GraphicsDevice, Width, Height);
        }

        [MonoModConstructor]
        internal patch_VirtualTexture(ModAsset metadata) {
            Metadata = metadata;
            Name = metadata.PathVirtual;
            // if (!Preload(force: Everest.Flags.IsHeadless) && !Everest.Flags.IsHeadless)
                Reload();
            if (Everest.Flags.IsHeadless)
                Texture_Unsafe = new Texture2D(Engine.Graphics.GraphicsDevice, Width, Height);
        }


        [MemberNotNull(nameof(Texture_Unsafe))]
        private void Load(Func<Texture2D> cb) {
            Texture_Unsafe = cb();
        }

        private bool LoadImmediately => MainThreadHelper.IsMainThread;

        [MemberNotNull(nameof(Texture_Unsafe))]
        internal override sealed void Reload() {
            if (Everest.Flags.IsHeadless) {
                Texture_Unsafe = new Texture2D(Engine.Graphics.GraphicsDevice, 1, 1);
                return;
            }
            
            Unload();
        
            if (Metadata != null) {
                Stream stream = Metadata.Stream;
                if (stream != null) {
                    bool premul = false; // Assume unpremultiplied by default.
                    if (Metadata.TryGetMeta(out TextureMeta meta))
                        premul = meta.Premultiplied;
                    Load(TextureContentHelper.LoadFromStream(stream, premul));
                } else if (Fallback != null) {
                    ((patch_VirtualTexture) (object) Fallback).Reload();
                    Texture_Unsafe = Fallback.Texture!;
                } else {
                    throw new InvalidOperationException();
                }
            } else if (string.IsNullOrEmpty(Path)) {
                Load(TextureContentHelper.LoadFromSizeAndColor(Width, Height, color));
            } else {
                Load(TextureContentHelper.LoadFromPath(Path));
            }
        
            Texture2D tex = Texture_Unsafe;
            Width = tex.Width;
            Height = tex.Height;
        }

        private bool CanPreload {
            get {
                if (!string.IsNullOrEmpty(Path)) {
                    string extension = System.IO.Path.GetExtension(Path);
                    if (extension == ".data") {
                        return true;
                    } else if (extension == ".png") {
                        return true;
                    } else {
                        return false;

                    }

                } else if (Metadata != null) {
                    if (Metadata.Format == "png") {
                        return true;
                    } else {
                        return false;
                    }
                }

                return false;
            }
        }

        private bool Preload(bool force = false) {
            if (!CoreModule.Settings.LazyLoading && !force) {
                return false;
            }

            // Preload the width / height, and if needed, the entire texture (not actually done currently though).

            if (!string.IsNullOrEmpty(Path)) {
                string extension = System.IO.Path.GetExtension(Path);
                if (extension == ".data") {
                    // Easy.
                    using (FileStream stream = File.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
                    using (BinaryReader reader = new BinaryReader(stream)) {
                        Width = reader.ReadInt32();
                        Height = reader.ReadInt32();
                    }
                    return true;

                } else if (extension == ".png") {
                    // Hard.
                    using (FileStream stream = File.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
                        return PreloadSizeFromPNG(stream, Path);

                } else {
                    // .xnb and other file formats - impossible.
                    return false;

                }

            } else if (Metadata != null) {
                if (Metadata.Format == "png") {
                    // Hard.
                    using (Stream stream = Metadata.Stream)
                        return PreloadSizeFromPNG(stream, $"{Metadata.PathVirtual} (mod {Metadata.Source.Mod?.Name ?? "*unknown*"})");

                } else {
                    // .xnb and other file formats - impossible.
                    return false;
                }
            }

            return false;
        }

        private bool PreloadSizeFromPNG(Stream stream, string path) {
            using (BinaryReader reader = new BinaryReader(stream)) {
                ulong magic = reader.ReadUInt64();
                if (magic != 0x0A1A0A0D474E5089U) {
                    Logger.Error("vtex", $"Failed preloading PNG: Expected magic to be 0x0A1A0A0D474E5089, got 0x{magic.ToString("X16")} - {path}");
                    return false;
                }
                uint length = reader.ReadUInt32();
                if (length != 0x0D000000U) {
                    Logger.Error("vtex", $"Failed preloading PNG: Expected first chunk length to be 0x0D000000, got 0x{length.ToString("X8")} - {path}");
                    return false;
                }
                uint chunk = reader.ReadUInt32();
                if (chunk != 0x52444849U) {
                    Logger.Error("vtex", $"Failed preloading PNG: Expected IHDR marker 0x52444849, got 0x{chunk.ToString("X8")} - {path}");
                    return false;
                }
                Width = SwapEndian(reader.ReadInt32());
                Height = SwapEndian(reader.ReadInt32());
                return true;
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

        /// <summary>
        /// If the VirtualTexture originates from a mod, get the mod asset metadata.
        /// </summary>
        [Obsolete("Use VirtualTexture.Metadata instead.")]
        public static ModAsset GetMetadata(this VirtualTexture self)
            => ((patch_VirtualTexture) (object) self).Metadata;

        /// <summary>
        /// Set a fallback texture in case the texture becomes unavailable on reload.
        /// </summary>
        [Obsolete("Use VirtualTexture.Fallback instead.")]
        public static void SetFallback(this VirtualTexture self, VirtualTexture fallback)
            => ((patch_VirtualTexture) (object) self).Fallback = fallback;

    }
}
