#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null

// #define FTL_DEBUG
// #define FTL_VERIFY

#if DEBUG || FTL_DEBUG
#define FTL_VERIFY
#endif

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Monocle {
    class patch_VirtualTexture : patch_VirtualAsset {

        // We're effectively in VirtualAsset, but still need to "expose" private fields to our mod.

        // Let's make the fixed buffers non-readonly and thread-static.
        // FIXME for MonoMod: Replacing fields is broken? Not critical though.
        // [MonoModRemove]
        // internal static readonly byte[] bytes;
        [ThreadStatic]
        [MonoModLinkFrom("System.Byte[] Monocle.VirtualTexture::bytes")]
        internal static byte[] bytesSafe;
        internal const int bytesSize = 512 * 1024; // 524288
        internal const int bytesCheckSize = 512 * 1024 - 32; // 524256

        // This one isn't thread-static as it's borrowed whenever necessary.
        // [MonoModRemove]
        // internal static readonly byte[] buffer;
        [MonoModLinkFrom("System.Byte[] Monocle.VirtualTexture::buffer")]
        internal static byte[] bufferSafe;
        internal static object bufferLock;
        internal const int bufferSize = 4096 * 4096 * 4; // 67108864

        private static bool ftlEnabled;
        private static object ftlLock;
        private static ManualResetEventSlim ftlFree;
        private static ManualResetEventSlim ftlFinish;
        private static volatile uint ftlLimit;
        private static volatile uint ftlUsed;
        private static volatile int ftlTotalCount;
        private static volatile int ftlUsedCount;
#if FTL_VERIFY
        private static HashSet<patch_VirtualTexture> ftlTotalCountVerify;
        private static HashSet<patch_VirtualTexture> ftlUsedCountVerify;
#endif

        private static extern void orig_cctor();
        [MonoModConstructor]
        private static void cctor() {
            orig_cctor();

            bufferLock = new object();

            ftlLock = new object();
            ftlFree = new ManualResetEventSlim(true);
            ftlFinish = new ManualResetEventSlim(true);
#if FTL_VERIFY
            ftlTotalCountVerify = new HashSet<patch_VirtualTexture>();
            ftlUsedCountVerify = new HashSet<patch_VirtualTexture>();
#endif
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
                        if (_Texture_QueuedLoadLock != queuedLoadLock)
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
                        return _Texture_QueuedLoad.Result;
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
        private ValueTask<Texture2D> _Texture_QueuedLoad;
        private bool _Texture_FTLCount;
        private bool _Texture_FTLLoading;
        private uint _Texture_FTLSize;

        public ModAsset Metadata;

        public VirtualTexture Fallback;

        public static void StartFastTextureLoading(long limit) {
            lock (ftlLock) {
                if (limit >= uint.MaxValue)
                    ftlLimit = uint.MaxValue;
                else
                    ftlLimit = (uint) limit;
                ftlEnabled = true;
            }
        }

        public static void StopFastTextureLoading() {
            lock (ftlLock) {
                ftlEnabled = false;
            }
        }

        private void CountFastTextureLoad() {
            // This should ALWAYS be called from the main thread.
            lock (ftlLock) {
                if (_Texture_FTLCount)
                    return;
#if FTL_VERIFY
                if (!ftlTotalCountVerify.Add(this))
                    throw new Exception($"FTL count total verify failed for texture \"{Name}\"");
#endif
#if FTL_DEBUG
                Console.WriteLine($"FTL Count   {Name} (queue: {ftlCount + 1})");
#endif

                _Texture_FTLCount = true;
                ftlTotalCount++;
            }
        }

        private void GrabFastTextureLoad() {
            // This should ALWAYS be called from the task.
            long size = Width * Height * 4;

            if (size == 0)
                throw new Exception($"Fast texture loading encountered zero-size texture: {Name}");

            // Smaller textures might contribute to fragmentation.
            // Let's allow squeezing small textures into small holes, but make them take up lots of space.
            // 512 * 512 * 4 = 1 MB
            if (size < 512 * 512 * 4)
                size = 512 * 512 * 4;
            // Add some artificial overhead (DotNetZip, .data file buffer, thread local data, ...).
            size += 1024 * 1024;
            // Guesstimate somewhere between the already estimated size and the next power of two.
            long sizePOT = size;
            sizePOT--;
            sizePOT |= sizePOT >> 1;
            sizePOT |= sizePOT >> 2;
            sizePOT |= sizePOT >> 4;
            sizePOT |= sizePOT >> 8;
            sizePOT |= sizePOT >> 16;
            sizePOT |= sizePOT >> 32;
            sizePOT++;
            size += (long) ((sizePOT - size) * 0.3);

            if (size >= ftlLimit || size >= uint.MaxValue)
                // throw new Exception($"Fast texture loading encountered an oversized texture: {Name} (estimated {size} bytes with overhead)");
                size = ftlLimit;

            // Don't wait inside of the lock or we will risk making other textures wait, even those which would fit.
            // Note that this could theoretically hold back fitting textures waiting to be tasked.
            Rewait:
            while (size <= ftlLimit ? ftlUsed + size > ftlLimit : ftlUsedCount > 1) {
                ftlFree.Wait();
                ftlFree.Reset();
            }

            lock (ftlLock) {
                if (size <= ftlLimit ? ftlUsed + size > ftlLimit : ftlUsedCount > 1)
                    goto Rewait;

#if FTL_VERIFY
                if (!ftlUsedCountVerify.Add(this))
                    throw new Exception($"FTL grab used verify failed for texture \"{Name}\"");
#endif
#if FTL_DEBUG
                Console.WriteLine($"FTL TryGrab {Name} {size} {ftlUsed + size <= ftlLimit} (avail: {ftlLimit - ftlUsed - size})");
#endif

                ftlFinish.Reset();
                ftlUsedCount++;
                ftlUsed += _Texture_FTLSize = (uint) size;
                _Texture_FTLLoading = true;
            }
        }

        private void FreeFastTextureLoad() {
            // This should ALWAYS be called from the main thread.
            lock (ftlLock) {
                if (!_Texture_FTLLoading) {
                    if (_Texture_FTLCount) {
#if FTL_VERIFY
                        if (ftlUsedCountVerify.Contains(this))
                            throw new Exception($"FTL cancel unused verify failed for texture \"{Name}\"");
                        if (!ftlTotalCountVerify.Remove(this))
                            throw new Exception($"FTL cancel total verify failed for texture \"{Name}\"");
#endif
#if FTL_DEBUG
                        Console.WriteLine($"FTL Cancel  {Name} (remain: {ftlCount - 1})");
#endif
                        ftlTotalCount--;
                        if (ftlTotalCount == 0)
                            ftlFinish.Set();
                        _Texture_FTLCount = false;
                    }
                    return;
                }

#if FTL_VERIFY
                if (!ftlUsedCountVerify.Remove(this))
                    throw new Exception($"FTL free used verify failed for texture \"{Name}\"");
                if (!ftlTotalCountVerify.Remove(this))
                    throw new Exception($"FTL free total verify failed for texture \"{Name}\"");
#endif
#if FTL_DEBUG
                Console.WriteLine($"FTL Free    {Name} {size} (remain: {ftlCount - 1})");
#endif

                uint size = _Texture_FTLSize;
                _Texture_FTLLoading = false;
                _Texture_FTLSize = 0;
                ftlUsed -= size;
                ftlUsedCount--;
                ftlTotalCount--;
                if (ftlTotalCount == 0)
                    ftlFinish.Set();
                ftlFree.Set();
                _Texture_FTLCount = false;
            }
        }

        public static void WaitFinishFastTextureLoading() {
            // This should ALWAYS be called from the loader thread.
            lock (ftlLock) {
                ftlEnabled = false;
                // Don't set the limit as there could still be grabs happening afterwards.
                // ftlLimit = 0;
            }
            // Lock shouldn't be necessary, all TryGrabs should've happened beforehand.
            // On the contrary, lock would make this / TryGrab / Free prone to deadlocks.
            ftlFinish.Wait();
        }

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
                    tex = _Texture_QueuedLoad.Result;
                }
            }

            Texture_Unsafe = null;
            if (tex == null || tex.IsDisposed)
                return;

            if (!(CoreModule.Settings.ThreadedGL ?? Everest.Flags.PreferThreadedGL) && !MainThreadHelper.IsMainThread) {
                MainThreadHelper.Schedule(() => tex.Dispose());
            } else {
                tex.Dispose();
            }
        }

        internal bool LoadImmediately => 
            !_Texture_FTLLoading && ((CoreModule.Settings.ThreadedGL ?? Everest.Flags.PreferThreadedGL) || MainThreadHelper.IsMainThread);
        internal bool Load(bool wait, Func<Texture2D> load) {
            if (LoadImmediately) {
                Texture_Unsafe?.Dispose();
                Texture_Unsafe = load();
                FreeFastTextureLoad();
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
                        if (_Texture_QueuedLoadLock != queuedLoadLock) {
                            _load = null;
                            FreeFastTextureLoad();
                            return Texture_Unsafe;
                        }
                        // NOTE: If something dares to change texture info on the fly, GOOD LUCK.
                        Texture_Unsafe?.Dispose();
                        Texture_Unsafe = tex = _load();
                        FreeFastTextureLoad();
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

                _Texture_QueuedLoad = MainThreadHelper.Schedule(load, forceQueue: !_Texture_FTLLoading);
            }

            if (wait || _Texture_Requesting)
                _ = _Texture_QueuedLoad.Result;

            return false;
        }

        // Same signature as .NET Core Unsafe variant, which also matches IL expectations.
        // This should get replaced with initblk at the call site in Reload as PatchInitblk is used
        [MonoModIgnore]
        private static unsafe extern void _initblk(void* startAddress, byte value, uint byteCount);

        [MonoModReplace]
        [PatchInitblk]
        internal override unsafe void Reload() {
            // Unload task might end up conflicting with Reload - let's instead force-unload in Load.
            // Unload();

            // Handle already queued loads appropriately.
            object queuedLoadLock = _Texture_QueuedLoadLock;
            if (queuedLoadLock != null && !_Texture_Reloading) {
                lock (queuedLoadLock) {
                    // Queued task finished just in time.
                    if (_Texture_QueuedLoadLock != queuedLoadLock)
                        return;

                    // If we still can, cancel the queued load, then proceed with lazy-loading.
                    if (MainThreadHelper.IsMainThread)
                        _Texture_QueuedLoadLock = null;
                }

                if (!MainThreadHelper.IsMainThread) {
                    // Otherwise wait for it to get loaded, don't reload twice. (Don't wait locked!)
                    _ = _Texture_QueuedLoad.Result;
                    return;
                }
            }

            if (ftlEnabled && CanPreload && (Metadata?.StreamAsync ?? true) &&
                !_Texture_Reloading && !_Texture_Requesting) {
                // Preload as we need to know the texture size WITHOUT ALLOCATING SPACE.
                // ... also because the texture size is required in the calling ctx past Reload.
                if (Preload(true)) {
                    CountFastTextureLoad();
                    lock (queuedLoadLock = new object()) {
                        _Texture_QueuedLoadLock = queuedLoadLock;
                        _Texture_QueuedLoad = new ValueTask<Texture2D>(Task.Run(() => {
                            try {
                                lock (queuedLoadLock) {
                                    // Queued load cancelled or replaced with another queued load.
                                    if (_Texture_QueuedLoadLock != queuedLoadLock) {
                                        FreeFastTextureLoad();
                                        return Texture_Unsafe;
                                    }

                                    GrabFastTextureLoad();

                                    // NOTE: If something dares to change texture info on the fly, GOOD LUCK.
                                    _Texture_Reloading = true;
                                    Reload();
                                    if (_Texture_QueuedLoadLock == queuedLoadLock)
                                        _Texture_QueuedLoadLock = null;
                                    Texture2D tex = Texture_Unsafe;
                                    if (_Texture_UnloadAfterReload) {
                                        tex?.Dispose();
                                        tex = Texture_Unsafe;
                                        // ... can anything even swap the texture here?
                                        Texture_Unsafe = null;
                                        tex?.Dispose();
                                        _Texture_UnloadAfterReload = false;
                                    }
                                    return tex;
                                }
                            } catch (Exception e) {
                                Celeste.patch_Celeste.CriticalFailureHandler(e);
                                throw;
                            }
                        }));
                        return;
                    }
                }
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
                                    data = null;
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
                                    data = null;
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
                fixed (Color* ptr = data)
                    for (int i = 0; i < data.Length; i++)
                        ptr[i] = color;
                Load(false, () => {
                    Texture2D tex = new Texture2D(Engine.Instance.GraphicsDevice, Width, Height);
                    tex.SetData(data);
                    data = null;
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
                            byte[] read = bytesSafe ??= new byte[bytesSize];
                            stream.Read(read, 0, read.Length);

                            int pB = 0;
                            w = BitConverter.ToInt32(read, pB);
                            h = BitConverter.ToInt32(read, pB + 4);
                            bool hasAlpha = read[pB + 8] == 1;
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
                                    buffer = null;
                                    bufferPtr = Marshal.AllocHGlobal(size);
                                }
                                bufferStolen = false;
                            }

                            fixed (byte* from = read)
                            fixed (byte* bufferPin = buffer) {
                                byte* to = bufferGC ? bufferPin : (byte*) bufferPtr;
                                int* toI = (int*) to;
                                uint iB = 0;
                                uint iI = 0;

                                // Let's dupe the loop and move hasAlpha out, otherwise hasAlpha gets checked often.
                                if (hasAlpha) {
                                    while (iB < size) {
                                        uint linesize = from[pB];

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

                                        if (linesize > 1) {
                                            if (a == 0) {
                                                _initblk(to + iB + 4, 0, linesize * 4 - 4);
                                            } else {
                                                for (uint jI = iI + 1, end = iI + linesize; jI < end; jI++)
                                                    toI[jI] = toI[iI];
                                            }
                                        }

                                        iI += linesize;
                                        iB = iI * 4;

                                        if (pB > read.Length - 32) {
                                            int offset = read.Length - pB;
                                            for (int oB = 0; oB < offset; oB++) {
                                                from[oB] = from[pB + oB];
                                            }
                                            stream.Read(read, offset, read.Length - offset);
                                            pB = 0;
                                        }
                                    }

                                } else {
                                    while (iB < size) {
                                        uint linesize = from[pB];

                                        to[iB] = from[pB + 3];
                                        to[iB + 1] = from[pB + 2];
                                        to[iB + 2] = from[pB + 1];
                                        to[iB + 3] = 255;
                                        pB += 4;

                                        if (linesize > 1)
                                            for (uint jI = iI + 1, end = iI + linesize; jI < end; jI++)
                                                toI[jI] = toI[iI];

                                        iI += linesize;
                                        iB = iI * 4;

                                        if (pB > bytesCheckSize) {
                                            int offset = read.Length - pB;
                                            for (int oB = 0; oB < offset; oB++) {
                                                from[oB] = from[pB + oB];
                                            }
                                            stream.Read(read, offset, read.Length - offset);
                                            pB = 0;
                                        }
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
                                buffer = null;
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
                                buffer = null;
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
