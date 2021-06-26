#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Monocle {
    class patch_VirtualTexture : patch_VirtualAsset {

        // We're effectively in VirtualAsset, but still need to "expose" private fields to our mod.
        internal static readonly byte[] bytes;
        internal const int bytesSize = 524288;

        // Let's make the fixed buffer non-readonly.
        internal static readonly byte[] buffer;
        internal static byte[] bufferSafe;
        internal static object bufferLock;
        internal const int bufferSize = 4096 * 4096 * 4; // 67108864

        private static extern void orig_cctor();
        [MonoModConstructor]
        private static void cctor() {
            orig_cctor();

            bufferSafe = buffer;
            bufferLock = new object();
        }

        public string Path { get; private set; }
        private Color color;

        [MonoModLinkFrom("Microsoft.Xna.Framework.Graphics.Texture2D Monocle.VirtualTexture::Texture_Unsafe")]
        public Texture2D Texture;

        [MonoModRemove]
        public Texture2D Texture_Unsafe;

        [MonoModLinkFrom("Microsoft.Xna.Framework.Graphics.Texture2D Monocle.VirtualTexture::Texture")]
        public Texture2D Texture_Safe {
            get {
                // Handle already queued loads appropriately.
                object queuedLoadLock = _Texture_QueuedLoadLock;
                bool lazyForce = false;
                if (queuedLoadLock != null) {
                    lock (queuedLoadLock) {
                        // Queued task finished just in time.
                        if (_Texture_QueuedLoadLock == null)
                            return Texture_Unsafe;

                        // If we still can, cancel the queued load, then proceed with lazy-loading.
                        if (MainThreadHelper.IsMainThread) {
                            _Texture_QueuedLoadLock = null;
                            _Texture_Reloading = false;
                            lazyForce = true;
                        }
                    }

                    if (!MainThreadHelper.IsMainThread) { 
                        // Otherwise wait for it to get loaded. (Don't wait locked!)
                        while (!_Texture_QueuedLoad.IsValid)
                            Thread.Yield();
                        return _Texture_QueuedLoad.GetResult();
                    }
                }

                if (_Texture_Reloading || !(CoreModule.Settings.LazyLoading || lazyForce))
                    return Texture_Unsafe;

                // If we're accessing the texture from elsewhere (render), load lazily if required.
                if (Texture_Unsafe?.IsDisposed ?? true) {
                    _Texture_Requesting = true;
                    Reload();
                }

                return Texture_Unsafe;
            }
            set {
                Texture_Unsafe = value;
            }
        }

        private bool _Texture_Reloading;
        private bool _Texture_Requesting;
        private bool _Texture_UnloadAfterReload;
        private object _Texture_QueuedLoadLock;
        private MaybeAwaitable<Texture2D> _Texture_QueuedLoad;

        public ModAsset Metadata;

        public VirtualTexture Fallback;

        public static bool ForceTaskedParse;
        public static bool ForceQueuedLoad;

        [MonoModConstructor]
        [MonoModReplace]
        internal patch_VirtualTexture(string path) {
            Path = path;
            Name = path;
            if (!Preload())
                Reload();
        }

        [MonoModConstructor]
        [MonoModReplace]
        internal patch_VirtualTexture(string name, int width, int height, Color color) {
            Name = name;
            Width = width;
            Height = height;
            this.color = color;
            if (!Preload())
                Reload();
        }

        [MonoModConstructor]
        internal patch_VirtualTexture(ModAsset metadata) {
            Metadata = metadata;
            Name = metadata.PathVirtual;
            if (!Preload())
                Reload();
        }

        [MonoModReplace]
        internal override void Unload() {
            Texture2D tex = Texture_Unsafe;

            // Handle already queued loads appropriately.
            object queuedLoadLock = _Texture_QueuedLoadLock;
            if (queuedLoadLock != null) {
                bool gotLock = false;
                try {
                    Monitor.TryEnter(queuedLoadLock, ref gotLock);
                    if (gotLock) {
                        if (_Texture_QueuedLoadLock != null) {
                            // If we still can, cancel the queued load.
                            _Texture_QueuedLoadLock = null;
                        }
                    } else {
                        // Welp.
                        _Texture_UnloadAfterReload = true;
                        Monitor.TryEnter(queuedLoadLock, ref gotLock);
                        if (gotLock) {
                            // It might be too late - let's unload ourselves.
                            _Texture_UnloadAfterReload = false;
                        } else {
                            // The loader will still handle the request.
                            return;
                        }
                    }
                } finally {
                    if (gotLock)
                        Monitor.Exit(queuedLoadLock);
                }

                if (!MainThreadHelper.IsMainThread) {
                    // Otherwise wait for it to get loaded. (Don't wait locked!)
                    while (!_Texture_QueuedLoad.IsValid)
                        Thread.Yield();
                    tex = _Texture_QueuedLoad.GetResult();
                }
            }

            Texture_Unsafe = null;
            if (tex == null || tex.IsDisposed)
                return;

            if (!(CoreModule.Settings.ThreadedGL ?? Everest.Flags.PreferThreadedGL) && !MainThreadHelper.IsMainThread) {
                MainThreadHelper.Do(() => tex.Dispose());
            } else {
                tex.Dispose();
            }
        }

        internal bool LoadImmediately =>
            (_Texture_Requesting || !ForceQueuedLoad) &&
            ((CoreModule.Settings.ThreadedGL ?? Everest.Flags.PreferThreadedGL) || MainThreadHelper.IsMainThread);
        internal bool Load(bool wait, Func<Texture2D> load) {
            if (LoadImmediately) {
                Texture_Unsafe?.Dispose();
                Texture_Unsafe = load();
                return true;
            }

            // Let's queue a reload onto the main thread and call it a day.
            // Make sure to read the texture size immediately though!
            object queuedLoadLock;
            lock (queuedLoadLock = new object()) {
                _Texture_QueuedLoadLock = queuedLoadLock;

                Func<Texture2D> _load = load;
                load = () => {
                    Texture2D tex;
                    lock (queuedLoadLock) {
                        if (_Texture_QueuedLoadLock == null)
                            return Texture_Unsafe;
                        // NOTE: If something dares to change texture info on the fly, GOOD LUCK.
                        Texture_Unsafe?.Dispose();
                        Texture_Unsafe = tex = _load();
                        _Texture_QueuedLoadLock = null;
                    }
                    if (_Texture_UnloadAfterReload) {
                        tex?.Dispose();
                        tex = Texture_Unsafe;
                        // ... can anything even swap the texture here?
                        Texture_Unsafe = null;
                        tex?.Dispose();
                        _Texture_UnloadAfterReload = false;
                    }
                    return tex;
                };

                _Texture_QueuedLoad = ForceQueuedLoad ?
                    MainThreadHelper.GetForceQueue(load) :
                    MainThreadHelper.Get(load);
            }

            if (wait || _Texture_Requesting)
                _Texture_QueuedLoad.GetResult();

            return false;
        }

        [MonoModReplace]
        internal override unsafe void Reload() {
            // Unload task might end up conflicting with Reload - let's instead force-unload in Load.
            // Unload();

            // Handle already queued loads appropriately.
            object queuedLoadLock = _Texture_QueuedLoadLock;
            if (queuedLoadLock != null && !_Texture_Reloading) {
                lock (queuedLoadLock) {
                    // Queued task finished just in time.
                    if (_Texture_QueuedLoadLock == null)
                        return;

                    // If we still can, cancel the queued load, then proceed with lazy-loading.
                    if (MainThreadHelper.IsMainThread)
                        _Texture_QueuedLoadLock = null;
                }

                if (!MainThreadHelper.IsMainThread) {
                    // Otherwise wait for it to get loaded, don't reload twice. (Don't wait locked!)
                    while (!_Texture_QueuedLoad.IsValid)
                        Thread.Yield();
                    _Texture_QueuedLoad.GetResult();
                    return;
                }
            }

            if ((Metadata?.StreamAsync ?? true) && ForceTaskedParse &&
                !_Texture_Reloading && !_Texture_Requesting) {
                Preload(true);
                lock (queuedLoadLock = new object()) {
                    _Texture_QueuedLoadLock = queuedLoadLock;
                    _Texture_QueuedLoad = new MaybeAwaitable<Texture2D>(Task.Run(() => {
                        Texture2D tex;
                        lock (queuedLoadLock) {
                            if (_Texture_QueuedLoadLock == null)
                                return Texture_Unsafe;
                            // NOTE: If something dares to change texture info on the fly, GOOD LUCK.
                            _Texture_Reloading = true;
                            Reload();
                            if (_Texture_QueuedLoadLock == queuedLoadLock)
                                _Texture_QueuedLoadLock = null;
                            tex = Texture_Unsafe;
                        }
                        if (_Texture_UnloadAfterReload) {
                            tex?.Dispose();
                            tex = Texture_Unsafe;
                            // ... can anything even swap the texture here?
                            Texture_Unsafe = null;
                            tex?.Dispose();
                            _Texture_UnloadAfterReload = false;
                        }
                        return tex;
                    }).GetAwaiter());
                }
                return;
            }

            _Texture_Reloading = false;

            if (Metadata != null) {
                if (Metadata.StreamAsync || MainThreadHelper.IsMainThread) {
                    Stream stream = Metadata.Stream;
                    if (stream != null) {
                        using (stream) {
                            bool premul = false; // Assume unpremultiplied by default.
                            if (Metadata.TryGetMeta(out TextureMeta meta))
                                premul = meta.Premultiplied;

                            if (ContentExtensions.TextureSetDataSupportsPtr) {
                                int w, h;
                                IntPtr dataPtr;
                                if (premul)
                                    ContentExtensions.LoadTextureRaw(Celeste.Celeste.Instance.GraphicsDevice, stream, out w, out h, out dataPtr);
                                else
                                    ContentExtensions.LoadTextureLazyPremultiply(Celeste.Celeste.Instance.GraphicsDevice, stream, out w, out h, out dataPtr);
                                stream.Dispose();
                                Width = w;
                                Height = h;
                                Load(false, () => {
                                    Texture2D tex = new Texture2D(Celeste.Celeste.Instance.GraphicsDevice, w, h, false, SurfaceFormat.Color);
                                    tex.SetData(dataPtr);
                                    ContentExtensions.UnloadTextureRaw(dataPtr);
                                    return tex;
                                });
                            } else {
                                int w, h;
                                byte[] data;
                                if (premul)
                                    ContentExtensions.LoadTextureRaw(Celeste.Celeste.Instance.GraphicsDevice, stream, out w, out h, out data);
                                else
                                    ContentExtensions.LoadTextureLazyPremultiply(Celeste.Celeste.Instance.GraphicsDevice, stream, out w, out h, out data);
                                stream.Dispose();
                                Width = w;
                                Height = h;
                                Load(false, () => {
                                    Texture2D tex = new Texture2D(Celeste.Celeste.Instance.GraphicsDevice, w, h, false, SurfaceFormat.Color);
                                    tex.SetData(data);
                                    return tex;
                                });
                            }
                        }

                    } else if (Fallback != null) {
                        ((patch_VirtualTexture) (object) Fallback).Reload();
                        Texture_Unsafe = Fallback.Texture;
                    }

                } else {
                    // This is ugly but if the asset doesn't like multithreading, so be it.
                    // Not even preloading will be beneficial here, and forget about GetMeta.
                    Load(true, () => {
                        using (Stream stream = Metadata.Stream) {
                            if (stream != null) {
                                bool premul = false; // Assume unpremultiplied by default.
                                if (Metadata.TryGetMeta(out TextureMeta meta))
                                    premul = meta.Premultiplied;

                                if (premul) {
                                    Texture2D tex = Texture2D.FromStream(Celeste.Celeste.Instance.GraphicsDevice, stream);
                                    return tex;
                                } else if (ContentExtensions.TextureSetDataSupportsPtr) {
                                    ContentExtensions.LoadTextureLazyPremultiply(Celeste.Celeste.Instance.GraphicsDevice, stream, out int w, out int h, out IntPtr dataPtr);
                                    Texture2D tex = new Texture2D(Celeste.Celeste.Instance.GraphicsDevice, w, h, false, SurfaceFormat.Color);
                                    tex.SetData(dataPtr);
                                    ContentExtensions.UnloadTextureRaw(dataPtr);
                                    return tex;
                                } else {
                                    ContentExtensions.LoadTextureLazyPremultiply(Celeste.Celeste.Instance.GraphicsDevice, stream, out int w, out int h, out byte[] data);
                                    Texture2D tex = new Texture2D(Celeste.Celeste.Instance.GraphicsDevice, w, h, false, SurfaceFormat.Color);
                                    tex.SetData(data);
                                    return tex;
                                }

                            } else if (Fallback != null) {
                                ((patch_VirtualTexture) (object) Fallback).Reload();
                                return Fallback.Texture;
                            }

                            return null;
                        }
                    });
                }

            } else if (string.IsNullOrEmpty(Path)) {
                Color[] data = new Color[Width * Height];
                fixed (Color* ptr = data) {
                    for (int i = 0; i < data.Length; i++) {
                        ptr[i] = color;
                    }
                }
                Load(false, () => {
                    Texture2D tex = new Texture2D(Engine.Instance.GraphicsDevice, Width, Height);
                    tex.SetData(data);
                    return tex;
                });

            } else {
                int w, h;
                bool bufferGC = !ContentExtensions.TextureSetDataSupportsPtr;
                byte[] buffer = null;
                IntPtr bufferPtr = IntPtr.Zero;
                bool bufferStolen = false;
                switch (System.IO.Path.GetExtension(Path)) {
                    case ".data":
                        using (FileStream stream = File.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path))) {
                            // Vanilla has got a static readonly byte[] bytes of fixed length - currently 524288
                            // Luckily we can read more chunks on demand.
                            byte[] bytes = LoadImmediately ?
                                patch_VirtualTexture.bytes :
                                new byte[bytesSize];
                            stream.Read(bytes, 0, bytesSize);

                            int pB = 0;
                            w = BitConverter.ToInt32(bytes, pB);
                            h = BitConverter.ToInt32(bytes, pB + 4);
                            bool hasAlpha = bytes[pB + 8] == 1;
                            pB += 9;

                            Width = w;
                            Height = h;

                            int size = w * h * 4;
                            // Vanilla has got a static readonly byte[] buffer of fixed length - currently 67108864
                            // Ideally there should be only a single texture using the max-sized buffer.
                            if (size == bufferSize) {
                                lock (bufferLock) {
                                    buffer = bufferSafe;
                                    bufferSafe = null;
                                    bufferStolen = true;
                                    bufferGC = true;
                                }
                            }
                            if (buffer == null) {
                                if (bufferGC) {
                                    buffer = new byte[size];
                                } else {
                                    buffer = new byte[0];
                                    bufferPtr = Marshal.AllocHGlobal(size);
                                }
                                bufferStolen = false;
                            }

                            fixed (byte* from = bytes)
                            fixed (byte* bufferPin = buffer) {
                                byte* to = bufferGC ? bufferPin : (byte*) bufferPtr;
                                int* toI = (int*) to;
                                int iB = 0;
                                int iI = 0;

                                while (iB < size) {
                                    int linesize = from[pB];

                                    if (hasAlpha) {
                                        byte a = from[pB + 1];
                                        if (a > 0) {
                                            to[iB] = from[pB + 4];
                                            to[iB + 1] = from[pB + 3];
                                            to[iB + 2] = from[pB + 2];
                                            to[iB + 3] = a;
                                            pB += 5;
                                        } else {
                                            toI[iI] = 0;
                                            pB += 2;
                                        }
                                    } else {
                                        to[iB] = from[pB + 3];
                                        to[iB + 1] = from[pB + 2];
                                        to[iB + 2] = from[pB + 1];
                                        to[iB + 3] = 255;
                                        pB += 4;
                                    }

                                    if (linesize > 1) {
                                        // memset would be nice but initblk is either IL-only or Unsafe.*-via-Core-only
                                        for (int jI = iI + 1, end = iI + linesize; jI < end; jI++)
                                            toI[jI] = toI[iI];
                                    }

                                    iI += linesize;
                                    iB = iI * 4;

                                    if (pB > bytesSize - 32) {
                                        int offset = bytesSize - pB;
                                        for (int oB = 0; oB < offset; oB++) {
                                            from[oB] = from[pB + oB];
                                        }
                                        stream.Read(bytes, offset, bytesSize - offset);
                                        pB = 0;
                                    }
                                }
                            }
                        }
                        Load(false, () => {
                            Texture2D tex = new Texture2D(Celeste.Celeste.Instance.GraphicsDevice, w, h);
                            // This is on the main thread so buffer should be consumed by SetData, reusable afterwards.
                            if (bufferGC) {
                                tex.SetData(buffer);
                                if (bufferStolen)
                                    bufferSafe = buffer;
                            } else {
                                tex.SetData(bufferPtr);
                                Marshal.FreeHGlobal(bufferPtr);
                            }
                            return tex;
                        });
                        break;

                    case ".png":
                        if (bufferGC) {
                            using (FileStream stream = File.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
                                ContentExtensions.LoadTextureLazyPremultiply(Celeste.Celeste.Instance.GraphicsDevice, stream, out w, out h, out buffer);
                            Width = w;
                            Height = h;
                            Load(false, () => {
                                Texture2D tex = new Texture2D(Celeste.Celeste.Instance.GraphicsDevice, w, h, false, SurfaceFormat.Color);
                                tex.SetData(buffer);
                                return tex;
                            });
                        } else {
                            using (FileStream stream = File.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
                                ContentExtensions.LoadTextureLazyPremultiply(Celeste.Celeste.Instance.GraphicsDevice, stream, out w, out h, out bufferPtr);
                            Width = w;
                            Height = h;
                            Load(false, () => {
                                Texture2D tex = new Texture2D(Celeste.Celeste.Instance.GraphicsDevice, w, h, false, SurfaceFormat.Color);
                                tex.SetData(bufferPtr);
                                ContentExtensions.UnloadTextureRaw(bufferPtr);
                                return tex;
                            });
                        }
                        break;

                    case ".xnb":
                        Load(true, () => Engine.Instance.Content.Load<Texture2D>(Path.Replace(".xnb", "")));
                        break;

                    default:
                        Load(true, () => {
                            using (FileStream stream = File.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
                                return Texture2D.FromStream(Engine.Graphics.GraphicsDevice, stream);
                        });
                        break;
                }
            }

            Texture2D tex = Texture_Unsafe;
            if (tex != null) {
                Width = tex.Width;
                Height = tex.Height;
            }

            _Texture_Requesting = false;
        }

        private bool Preload(bool force = false) {
            if (!CoreModule.Settings.LazyLoading && !force) {
                return false;
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
                    return true;

                } else if (extension == ".png") {
                    // Hard.
                    using (FileStream stream = File.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
                        GetSizeFromPNG(stream);
                    return true;

                } else {
                    // .xnb and other file formats - impossible.
                    return false;

                }

            } else if (Metadata != null) {
                if (Metadata.Format == "png") {
                    // Hard.
                    using (Stream stream = Metadata.Stream)
                        GetSizeFromPNG(stream);
                    return true;

                } else {
                    // .xnb and other file formats - impossible.
                    return false;
                }
            }

            return false;
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
