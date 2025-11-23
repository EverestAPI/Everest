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
                    Reload(true);
                    
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

        private readonly object _textureLock;
        // Queued upload to gpu of the texture, assumed to always run on main thread
        private Task? _queuedLoad;
        private QueuedLoad? _queuedLoadAct;
        private readonly object _reloadLock;
        private volatile bool _reloadInProgress;
        private bool isPreloaded;
        private Exception? asyncFault;
        private bool _textureOverridden;
        private ulong _reloadVersion;

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

        // This function assumes it is executing on at most one thread at a time
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
                            // TODO: This is a bit wasteful, especially if we moved out of main thread to load asynchronously, maybe check asynchronousness beforehand?
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

        // Attempts to start a Reload on the current thread or returns if one is already ongoing.
        // If block is true it guarantees that either: a texture is assigned after its load or that _queuedLoad is not null and has pending work.
        // If block is false it only guarantees that a Reload is happening on some thread.
        private void Reload(bool block) {
            if (_reloadInProgress && !block) {
                // Someone has the lock, and we are not going to block anyway, so return early
                return;
            } 
            if (FtlToggle && isPreloaded && !block && MainThreadHelper.IsMainThread) {
                // This is the main asynchronous FTL entry point
                // isPreloaded is required to be true so we can have some knowledge of the memory usage of the load
                Task.Run(() => {
                    Reload(false);
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

        [MonoModReplace]
        internal override sealed void Reload() {
            Reload(false);
        }
        
        // TODO: Get rid of the replace and ilpatch to use Texture_Unsafe
        [MonoModReplace]
        internal override void Unload() {
            SoftUnload(true, out _);
        }

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

        // TODO: Get rid of the replace and ilpatch to use Texture_Unsafe
        [MonoModReplace]
        public override void Dispose() {
            Unload();
            // Texture_Unsafe = null;
            patch_VirtualContent.Remove(this);
        }

        private bool Preload(bool force = false) {
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
        
        public class QueuedLoad {
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
