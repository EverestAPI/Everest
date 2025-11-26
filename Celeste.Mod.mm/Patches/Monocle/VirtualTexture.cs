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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

#nullable enable

// FTL v2:
// This file hosts the implementation of FTL v2, Fast Texture Loading version 2.
// 
// Its main goal is to offload the work to other threads of reading the texture data, unpacking it 
// and copying it to an array for upload to the GPU, so that the loading process is sped up by
// asynchronously loading textures. Although the GPU uploads are synced back to the main thread.
// This last detail also fixes a bug in vanilla where GPU uploads could crash the game on rare occasions
// on Nvidia gpus.
//
// It is enabled via the static field FtlToggle, so while that is false all loads will happen synchronously.
// It is important to note that only loads invoked from the main thread will be offloaded to other threads,
// this due to the assumption that if loading happens intentionally on a separate thread it is because loads
// are meant to happen on that separate thread only.
//
// You will see that most work is sent to the TextureContentHelper, a helper class whose goal is to do the
// actual loading of the data, as well as capping the current memory usage to not freeze the system (since
// FTL simply offloads all loads onto other threads without checking system pressure).
//
// FTL also implements the ability to lazy load all textures: when a texture is created only its size is read
// and no loading actually occurs, that only when its Texture2D is accessed for the first time. This leads to
// massive gains on the game loading speed but at the cost of constant stutters on gameplay, thus it is not recommended
// unless there are massive memory constraints. Although some mods may use the event
// Everest.Events.VirtualTexture.ShouldForceLazyLoad to force a lazy load for specific textures, in case it has some extra 
// knowledge of when the texture will be used.
// There's also an event to track when a lazily loaded texture is loaded too late (on access) and may cause a gameplay stutter:
// Everest.Events.VirtualTexture.OnLazyLoad.
// It is important to note that not all textures can be preloaded (have its size loaded without fully loading the texture
// itself), for those textures lazy loading will simply not happen.
//
// The FTL v2 implementation also has the added benefit of making VirtualTexture completely thread-safe.
// This does not prevent race conditions on user code though.
//
// For backwards-compatibility sake it is allowed to resize textures (change its Width and Height) properties if and only if 
// those were made using the (string name, int width, int height, Color color) constructor. All resizes will implicitly call
// a reload and consecutively erase the contents if those were somehow modified. An additional property is provided so both
// dimensions can be changed without calling two separate reloads: VirtualTexture.Size.
// For backwards-compatibility sake it is also allowed to override the Texture2D that this VirtualTexture owns, doing so will
// grant ownership of the newly given Texture2D to the VirtualTexture meaning if it were to be `Unload`ed the new Texture2D 
// would be disposed. While the texture is overriden Reloads are nullified. If a reload were to be in progress when the texture
// is overriden, it will be canceled and have no effect. If the texture is overriden while already being overriden the
// VirtualTexture will take ownership of the new one and leave ownership of the old one. Finally, if the texture is overriden
// with a null texture it would have the exact same effect as an Unload call.
// 
// Finally, this class is also tasked with the headless mode loading optimizations, where all textures which can be preloaded
// will have its Texture2D set to a 1x1 texture, this is purely for performance’s sake. Textures which cannot be preloaded will
// be loaded as usual.
namespace Monocle {
    class patch_VirtualTexture : patch_VirtualAsset {

        private string? _path;
        public string? Path {
            [MonoModReplace] get => _path;
            [MonoModReplace]
            private set {
                if (_textureKind != TextureKind.FileSystem || _path != null) 
                    throw new InvalidOperationException("Cannot assign to path!");
                _path = value;
            }
        }
        private Color color;
        private int _width;
        private int _height;

        // Intermediary overridable property to call reloads on change
        protected override int InnerWidth {
            get => _width;
            set {
                // It makes no sense to write to this on the other texture kinds since the value will get overwritten after the reload anyway
                if (_textureKind != TextureKind.SizeDefined)
                    throw new InvalidOperationException("Resizing a VirtualTexture is only allowed for size defined textures!");
                lock (_reloadLock) {
                    _width = value;
                }
                Unload();
                Reload(false);
            }
        }

        // Intermediary overridable property to call reloads on change
        protected override int InnerHeight {
            get => _height;
            set {
                if (_textureKind != TextureKind.SizeDefined)
                    throw new InvalidOperationException("Resizing a VirtualTexture is only allowed for size defined textures!");
                lock (_reloadLock) {
                    _height = value;
                }
                Unload();
                Reload(false);
            }
        }

        // Helper property to modify both with and height without calling reload twice
        public Point Size {
            get => new(InnerWidth, InnerHeight);
            set {
                lock (_reloadLock) {
                    _width = value.X;
                    _height = value.Y;
                }
                Unload();
                Reload(false);
            }
        }

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
        private Texture2D? Texture_Unsafe;

        /// <summary>
        /// Returns the current texture, and forces a reload if necessary.
        /// </summary>
        /// <exception cref="AggregateException">Thrown if the reload happened asynchronously and there was an exception during it.</exception>
        [MonoModLinkFrom("Microsoft.Xna.Framework.Graphics.Texture2D Monocle.VirtualTexture::Texture")]
        public Texture2D? Texture_Safe {
            get {
                do {
                    // Return the texture if it is ready
                    if (Texture_Unsafe != null) {
                        lock (_textureLock) {
                            if (Texture_Unsafe != null)
                                return Texture_Unsafe;
                        }
                    }

                    // Check if the texture is null because failure
                    if (asyncFault != null) {
                        lock (_reloadLock) {
                            if (asyncFault != null)
                                throw new AggregateException("Exception during asynchronous texture load", asyncFault);
                        }
                    }
                    // If asyncFault is null but is about to become non-null we will check after the reload

                    // Otherwise try queuing a reload
                    Logger.Debug(nameof(VirtualTexture), $"Loading texture {Name ?? "(Unnamed)"} on texture access!");
                    Reload(true, true);
                    
                    // We could get a reload that sets this to null, this is an unfortunate case where we will void the error
                    // regardless we cannot do much about it, and it will likely error again
                    if (asyncFault != null) {
                        lock (_reloadLock) {
                            if (asyncFault != null)
                                throw new AggregateException("Exception during asynchronous texture load", asyncFault);
                        }
                    }

                    Task queuedLoad;
                    QueuedLoad queuedLoadAct;
                    // Prevent any texture swaps in the meanwhile
                    lock (_textureLock) {
                        if (_queuedLoad == null || _queuedLoad.IsCompleted) {
                            // If there's no task to use Texture_Unsafe cannot be swapped anymore here so this check is thread-safe
                            if (Texture_Unsafe != null)
                                return Texture_Unsafe;

                            continue; // We have got no texture, and we cannot wait for anything so try again
                        }
                        // Wait for the _queuedLoad, but not locked
                        queuedLoad = _queuedLoad;
                        queuedLoadAct = _queuedLoadAct!;
                    }

                    // Wait for the texture load, and check again if we have got anything to return
                    if (MainThreadHelper.IsMainThread) {
                        // But waiting for the load on the main thread would be disastrous (deadlock)
                        // so just run it directly, we are on the main thread anyway (the action is idempotent)
                        queuedLoadAct.Run();
                    } else {
                        queuedLoad.Wait();
                    }
                } while (true);
            }
            set {
                // It does not make much sense to assign to the texture, but some mods do, and vanilla allows for that to happen.
                // Un-synchronized assignments will often lead to race conditions, but there's not much we can do other than keep the state of this object valid.
                if (value == null) {
                    Unload();
                } else {
                    lock (_textureLock) {
                        _textureOverridden = true;
                        if (_queuedLoadAct != null) {
                            _queuedLoadAct.Cancel();
                            _queuedLoadAct = null;
                            _queuedLoad = null;
                        }
                        QueuedLoad.ImmediateAssign(this, value, true, _reloadVersion);
                        _reloadVersion++;
                    }
                }
            }
        }

        public readonly ModAsset? Metadata;

        public VirtualTexture? Fallback;

        private readonly TextureKind _textureKind;

        // Lock used to synchronize Texture_Unsafe reads and writes
        private readonly object _textureLock;
        // Queued upload to gpu of the texture, assumed to always run on main thread
        private Task? _queuedLoad;
        // The action of the task above, used to "steal" it when on main thread and run it directly
        private QueuedLoad? _queuedLoadAct;
        // Main lock used to synchronize loads and wait for them
        private readonly object _reloadLock;
        // Flag to know whether a reload is currently happening
        private volatile bool _reloadInProgress;
        // Whether the texture width and height could be determined ahead of time
        private bool isPreloaded;
        // Any exceptions thrown during the asynchronous load
        private Exception? asyncFault;
        // Whether the current Texture2D is overriden
        private bool _textureOverridden;
        // The reload version, used to track whether any more reloads (or texture overrides) happened in the meantime
        private ulong _reloadVersion;

        // The main FTL toggle, see this class header for all the details
        public static bool FtlToggle { get; internal set; }

        [MonoModConstructor]
        [MonoModReplace]
        internal patch_VirtualTexture(string path) {
            ArgumentException.ThrowIfNullOrEmpty(path, nameof(path));
            _textureKind = TextureKind.FileSystem;
            Path = path;
            Name = path;
            _textureLock = new object();
            _reloadLock = new object();
            CtorLoad();
        }

        [MonoModConstructor]
        [MonoModReplace]
        internal patch_VirtualTexture(string name, int width, int height, Color color) {
            ArgumentOutOfRangeException.ThrowIfLessThan(width, 1, nameof(width));
            ArgumentOutOfRangeException.ThrowIfLessThan(height, 1, nameof(height));
            _textureKind = TextureKind.SizeDefined;
            Name = name;
            _width = width;
            _height = height;
            this.color = color;
            _textureLock = new object();
            _reloadLock = new object();
            CtorLoad();
        }

        [MonoModConstructor]
        internal patch_VirtualTexture(ModAsset metadata) {
            ArgumentNullException.ThrowIfNull(metadata);
            _textureKind = TextureKind.ModAsset;
            Metadata = metadata;
            Name = metadata.PathVirtual;
            _textureLock = new object();
            _reloadLock = new object();
            CtorLoad();
        }

        // Extra setup common in all constructors
        private void CtorLoad() {
            if (Everest.Flags.IsHeadless) {
                bool preload = Preload();
                Everest.Events.VirtualTexture.OnShouldForceLazyLoad((VirtualTexture) (object) this);
                // If a preload is not possible just load the texture, even on headless
                // otherwise we risk having the wrong size, skipping loads entirely is just a
                // performance optimization
                if (!preload) {
                    Reload();
                    return;
                }
                // Big special case, this is the only other place where Texture_Unsafe gets assigned to
                // we are in the ctor, so there's no need to do anything safely
                Texture_Unsafe = new Texture2D(Engine.Graphics.GraphicsDevice, 1, 1);
                return;
            }
            // Only skip reloads with lazyloading and successful preloads
            if (!Preload() || (!Everest.Events.VirtualTexture.OnShouldForceLazyLoad((VirtualTexture) (object) this) && !CoreModule.Settings.LazyLoading))
                Reload();
        }

        /// <summary>
        /// Runs a callback in the main thread.
        /// </summary>
        /// <param name="ql">The queued load.</param>
        /// <param name="wait">Whether to wait.</param>
        private void RunSafely(QueuedLoad ql, bool wait = false) {
            // InnerReload checks makes sure _queuedLoad is null, and it maintains exclusive execution after that check
            // so this must hold
            if (_queuedLoad != null) throw new InvalidOperationException();
            if (LoadImmediately) {
                ql.Run();
                return;
            }

            ValueTask vt = MainThreadHelper.Schedule(ql.Run);
            // This is somewhat pointless, IsCompleted cant be true with the current LoadImmediately criteria
            if (vt.IsCompleted) {
                return;
            }

            Task t = vt.AsTask();
            lock (_textureLock) {
                _queuedLoad = t;
                _queuedLoadAct = ql;
            }
            if (wait) {
                t.Wait();
            }
            
            return;
        }

        // Make sure that MainThreadHelper.IsMainThread == true implies LoadImmediately == true
        private bool LoadImmediately => MainThreadHelper.IsMainThread;

        /// <summary>
        /// Critical part of a reload.
        /// This function assumes it is executing on at most one thread at a time.
        /// </summary>
        /// <param name="block">Whether to wait for the main thread transaction to complete before returning.</param>
        /// <exception cref="InvalidOperationException">On invalid cases.</exception>
        private void InnerReload(bool block = false) {
            asyncFault = null;
            // If the texture is overriden, reloads are pointless
            if (!SoftUnload(false, out ulong reloadVersion)) 
                return;
            // Important, this is the main entrypoint, and any code-path should lead to an eventual unique call to RunSafely or AssignTexture
            int preW = -1;
            int preH = -1;
            if (isPreloaded) {
                preW = _width;
                preH = _height;
            }

            switch (_textureKind) {
                case TextureKind.ModAsset: {
                    Debug.Assert(Metadata is not null);
                    Stream stream = Metadata.Stream;
                    if (stream != null) {
                        bool premul = false; // Assume unpremultiplied by default.
                        if (Metadata.TryGetMeta(out TextureMeta meta))
                            premul = meta.Premultiplied;
                        // If we have async streams, read async, otherwise, wrap everything in the RunSafely call and run the returned cb immediately
                        QueuedLoad ql;
                        if (Metadata.StreamAsync) {
                            ql = new QueuedLoad(this, TextureContentHelper.LoadFromStream(stream, premul, preW, preH), reloadVersion);
                        } else {
                            // This is a bit wasteful, especially if we moved out of main thread to load asynchronously, it's a rare edge case though and makes the code simpler
                            ql = new QueuedLoad(this, () => {
                                (Func<Texture2D> main, Action? cleanup) pair = TextureContentHelper.LoadFromStream(stream, premul, preW, preH);
                                Texture2D tex = pair.main();
                                pair.cleanup?.Invoke();
                                return tex;
                            }, null, reloadVersion);
                        }
                        RunSafely(ql, block);
                        return;
                    } else if (Fallback != null) {
                        // ReSharper disable once SuspiciousTypeConversion.Global
                        ((patch_VirtualTexture) (object) Fallback).Reload(true);
                        QueuedLoad.ImmediateAssign(this, Fallback.Texture!, false, reloadVersion);
                        return;
                    } else {
                        throw new InvalidOperationException("Cannot have null ModAsset stream without Fallback texture!");
                    }
                }
                case TextureKind.FileSystem: {
                    Debug.Assert(Path is not null);
                    RunSafely(new QueuedLoad(this, TextureContentHelper.LoadFromPath(Path, preW, preH), reloadVersion), block);
                    return;
                }
                case TextureKind.SizeDefined: {
                    Debug.Assert(_width > 0 && _height > 0);
                    RunSafely(new QueuedLoad(this, TextureContentHelper.LoadFromSizeAndColor(_width, _height, color), reloadVersion), block);
                    return;
                }
                default:
                    throw new UnreachableException();
            }
        }

        /// <summary>
        /// Attempts to start a Reload on the current thread or returns if one is already ongoing.
        /// </summary>
        /// <param name="block">
        /// When true it guarantees that either: a texture is assigned after its load or that <see cref="patch_VirtualTexture._queuedLoad"/> is not null and has pending work.
        /// When false it only guarantees that a Reload is happening on some thread.
        /// </param>
        /// <param name="isLazy">Only used to fire an event for mods that care when a texture may be loaded on access.</param>
        private void Reload(bool block, bool isLazy = false) {
            if (_reloadInProgress && !block) {
                // Someone has the lock, and we are not going to block anyway, so return early
                return;
            } 
            if (FtlToggle && isPreloaded && !block && MainThreadHelper.IsMainThread) {
                // This is the main asynchronous FTL entry point
                // isPreloaded is required to be true so we can have some knowledge of the memory usage of the load
                Task.Run(() => {
                    Reload(false, isLazy);
                });
                // Since we are not blocking, we are free to return whenever we want
                return;
            }
            retry:
            bool got = Monitor.TryEnter(_reloadLock);
            if (!got) {
                // Failing to acquire the lock does not guarantee a reload is going to happen since there are other acquires down below
                if (!_reloadInProgress) goto retry;
                if (block) {
                    // There has to be a better way to do this
                    Monitor.Enter(_reloadLock);
                    Monitor.Exit(_reloadLock);
                }
                return;
            }
            _reloadInProgress = true;
            try {
                if (isLazy)
                    Everest.Events.VirtualTexture.LazyLoad((VirtualTexture)(object)this);
                // Do not wait for the main thread to finish the load, it could deadlock if a blocking Reload is called on there
                InnerReload(false);
            } catch (Exception ex) {
                Logger.Error(nameof(VirtualTexture), $"Failed loading texture {Name ?? $"{_width}x{_height}"}!");
                Logger.LogDetailed(ex, nameof(VirtualTexture));
                asyncFault = ex;
                throw;
            } finally {
                _reloadInProgress = false;
                Monitor.Exit(_reloadLock);
            }
        }

        /// <summary>
        /// Causes a reload (or just load) of the texture, it may complete asynchronously.
        /// </summary>
        [MonoModReplace]
        internal override sealed void Reload() {
            Reload(false);
        }
        
        /// <summary>
        /// Unloads the texture from video memory.
        /// </summary>
        [MonoModReplace]
        internal override void Unload() {
            SoftUnload(true, out _);
        }

        /// <summary>
        /// Tries to unload the texture, may fail if the texture is overriden or there's a load waiting to complete.
        /// </summary>
        /// <param name="force">Forces the unload to succeed even if the texture is overridden or there's a pending load</param>
        /// <param name="reloadVersion">Returns the current reload version after the reload</param>
        /// <returns>Whether the unload was successful.</returns>
        private bool SoftUnload(bool force, out ulong reloadVersion) {
            lock (_textureLock) {
                reloadVersion = 0;
                if (!force) {
                    if (_textureOverridden || _queuedLoadAct != null) {
                        return false;
                    }
                }
                if (_queuedLoadAct != null) {
                    _queuedLoadAct.Cancel();
                    _queuedLoadAct = null;
                    _queuedLoad = null;
                }
                if (Texture_Unsafe is { IsDisposed: false }) {
                    Texture_Unsafe.Dispose();
                }
                Texture_Unsafe = null;
                _textureOverridden = false;
                reloadVersion = ++_reloadVersion;
                return true;
            }
        }

        // IL patch is possible here, is it worth it though? (IL should not change that much)
        /// <summary>
        /// Disposes the native resources and unregisters itself.
        /// </summary>
        [MonoModReplace]
        public override void Dispose() {
            Unload();
            // Texture_Unsafe = null;
            patch_VirtualContent.Remove(this);
        }

        /// <summary>
        /// Attempts to load the width and height of the texture without loading it as a whole.
        /// </summary>
        /// <returns>Whether the preload was successful</returns>
        private bool Preload() {
            // Preload the width / height, and if needed, the entire texture (not actually done currently though).

            switch (_textureKind) {
                case TextureKind.FileSystem: {
                    Debug.Assert(Path is not null);
                    string extension = System.IO.Path.GetExtension(Path);
                    if (extension == ".data") {
                        // Easy.
                        using (FileStream stream = File.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
                        using (BinaryReader reader = new BinaryReader(stream)) {
                            _width = reader.ReadInt32();
                            _height = reader.ReadInt32();
                        }
                        return isPreloaded = true;

                    } else if (extension == ".png") {
                        // Hard.
                        using (FileStream stream = File.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
                            return isPreloaded = PreloadSizeFromPNG(stream, Path);

                    } else {
                        // .xnb and other file formats - impossible.
                        return false;

                    }
                }
                case TextureKind.ModAsset: {
                    Debug.Assert(Metadata is not null);
                    if (Metadata.Format == "png") {
                        // Hard.
                        using (Stream stream = Metadata.Stream)
                            return isPreloaded = PreloadSizeFromPNG(stream, $"{Metadata.PathVirtual} (mod {Metadata.Source.Mod?.Name ?? "*unknown*"})");

                    } else {
                        // .xnb and other file formats - impossible.
                        return false;
                    }
                }
                case TextureKind.SizeDefined: {
                    Debug.Assert(_width != 0 && _height != 0);
                    // SizeDefined textures are already pre-loaded by definition
                    return isPreloaded = true;
                }
                default:
                    throw new UnreachableException();
            }
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
                _width = SwapEndian(reader.ReadInt32());
                _height = SwapEndian(reader.ReadInt32());
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

        /// <summary>
        /// Helper class to assign to Texture_Unsafe in a safe manner
        /// </summary>
        private class QueuedLoad {
            private bool hasRun;
            private readonly patch_VirtualTexture _vtex;
            private readonly Func<Texture2D>? _main;
            private readonly Texture2D? _immediateTexture;
            private readonly Action? _cleanup;
            private readonly ulong _reloadVersion;

            public QueuedLoad(patch_VirtualTexture vtex, (Func<Texture2D> main, Action? cleanup) pair, ulong reloadVersion) : this(vtex, pair.main, pair.cleanup, reloadVersion) {}
            public QueuedLoad(patch_VirtualTexture vtex, Func<Texture2D> main, Action? cleanup, ulong reloadVersion) {
                _vtex = vtex;
                _main = main;
                _cleanup = cleanup;
                _reloadVersion = reloadVersion;
            }

            public void Run() {
                lock (_vtex._textureLock) {
                    if (hasRun) return;
                    hasRun = true;
                    if (_vtex._textureOverridden || _vtex._reloadVersion != _reloadVersion) {
                        _cleanup?.Invoke();
                        return;
                    }
                    Texture2D tex = _main?.Invoke() ?? _immediateTexture!;
                    AssignTexture(tex);
                    _cleanup?.Invoke();
                }
            }

            // Must be called in the appropriate vtex lock
            public void Cancel() {
                if (hasRun) return;
                hasRun = true;
                _cleanup?.Invoke();
            }
            
            private void AssignTexture(Texture2D tex) {
                ArgumentNullException.ThrowIfNull(tex);
                _vtex.Texture_Unsafe = tex;
                _vtex._width = tex.Width;
                _vtex._height = tex.Height;
                // These callbacks may hold references to big memory arrays, so cut the references once we are done
                _vtex._queuedLoad = null;
                _vtex._queuedLoadAct = null;
            }

            public static void ImmediateAssign(patch_VirtualTexture vtex, Texture2D tex, bool force, ulong reloadVersion) {
                if (!force) {
                    lock (vtex._textureLock) {
                        if (vtex._textureOverridden) return;
                        ImmediateAssign(vtex, tex, true, reloadVersion);
                    }
                }
                ArgumentNullException.ThrowIfNull(tex);
                if (vtex._reloadVersion != reloadVersion) return;
                vtex.Texture_Unsafe = tex;
                vtex._width = tex.Width;
                vtex._height = tex.Height;
            }
        }

        private enum TextureKind {
            FileSystem,
            ModAsset,
            SizeDefined
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
