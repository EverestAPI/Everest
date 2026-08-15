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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;
using LTT = System.Lazy<System.Threading.Tasks.Task<Microsoft.Xna.Framework.Graphics.Texture2D>>;

#nullable enable

// FTL v2, notes on the design and implementation:
// This file hosts the implementation of FTL v2, Fast Texture Loading version 2.
// 
// Its main goal is to offload the work to other threads of reading the texture data, unpacking it 
// and copying it to an array for upload to the GPU, so that the loading process is sped up by
// asynchronously decoding textures. Although the GPU uploads are synced back to the main thread.
// This last detail also fixes a bug in vanilla where GPU uploads could crash the game on rare occasions
// on Nvidia gpus.
//
// It is enabled via the method TextureContentHelper.TryEnableFTL, so while that is false all loads
// will happen synchronously.
//
// You will see that most work is sent to the TextureContentHelper, a helper class whose goal is to do the
// actual loading of the data, as well as capping the current memory usage to not freeze the system.
//
// FTL also implements the ability to lazy load all textures: when a texture is created only its size is read
// and no loading actually occurs. This leads to massive gains on the game loading speed but at the cost of
// constant stutters on gameplay, thus it is not recommended unless there are massive memory constraints. Although
// some mods may use the event Everest.Events.VirtualTexture.ShouldForceLazyLoad to force a lazy load for specific
// textures in case it has some extra knowledge of when the texture will be used.
// There's also an event to track when a lazily loaded texture is loaded too late (on access) and may cause a gameplay stutter:
// Everest.Events.VirtualTexture.OnLazyLoad.
// Note that not all textures will be lazy loaded. Those which cannot be preloaded will just load normally (also skipping FTL).
//
// The FTL v2 implementation also has the added benefit of making VirtualTexture completely thread-safe.
// This does not prevent race conditions on user code though.
//
// For backwards-compatibility sake it is allowed to resize textures (change its Width and Height) properties if those were made
// using the (string name, int width, int height, Color color) constructor. All resizes will implicitly call a reload and
// erase the contents if those were somehow modified. 
// For backwards-compatibility sake it is also allowed to override the Texture2D that this VirtualTexture owns. Doing so will
// grant ownership of the newly given Texture2D to the VirtualTexture meaning if it were to be `Unload`ed or `Reload`ed the
// new Texture2D would be disposed. If the texture is overriden while already being overriden the VirtualTexture will take
// ownership of the new one and leave ownership of the old one. Finally, if the texture is overriden with a null texture it
// would have the exact same effect as an Unload call.
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
        private int _orig_width;
        private int _orig_height;

        // Makes sure _orig_width and _orig_height are updateable on SizeDefined textures
        protected override void HandleSizeChange() {
            if (_textureKind != TextureKind.SizeDefined)
                throw new InvalidOperationException("Resizing a VirtualTexture is only allowed for size defined textures!");
            lock (_textureLock) {
                _orig_width = _width;
                _orig_height = _height;
            }
        }

        // Texture is mapped to Texture_Safe, and we use _textureTask as the underlying field, so this is not needed
        [MonoModRemove] 
        public Texture2D? Texture;

        /// <summary>
        /// Returns the current texture, and forces a reload if necessary.
        /// </summary>
        /// <exception cref="AggregateException">Thrown if the reload happened asynchronously and there was an exception during it.</exception>
        [MonoModLinkFrom("Microsoft.Xna.Framework.Graphics.Texture2D Monocle.VirtualTexture::Texture")]
        public Texture2D? Texture_Safe {
            get {
                Texture2D? cachedTexture = _cachedTexture;
                if (cachedTexture != null) // Fast path
                    return cachedTexture;
                
                // Amortized slow path
                lock (_textureLock) {
                    if (!_textureTask.IsValueCreated) {
                        Everest.Events.VirtualTexture.LazyLoad((VirtualTexture) (object) this);
                        Logger.Debug(nameof(VirtualTexture), $"Loading texture {Name ?? "(Unnamed)"} on texture access!");
                    }
                    
                    // Never call _textureTask.Value.Result without knowing you're not on main thread or that its completed!
                    // Otherwise, we could deadlock!
                    return _cachedTexture = SafeWaitForTextureUnlocked();
                }
            }
            set {
                // It does not make much sense to assign to the texture, but some mods do, and vanilla allows for that to happen.
                // Un-synchronized assignments will often lead to race conditions, but there's not much we can do other than keep the state of this object valid.
                // Note that this property will never return null, thus we define assigning null to it as just unloading it.
                if (value == null) {
                    Unload();
                    return;
                }
                lock (_textureLock) {
                    CancelLoadUnlocked();
                    _textureTask = new LTT(Task.FromResult(value));
                    _cachedTexture = value;
                    _width = value.Width;
                    _height = value.Height;
                }
            }
        }

        public bool IsDisposed {
            [MonoModReplace]
            get => _disposed || 
                   !Celeste.Celeste.Instance.GraphicsDevice.IsDisposed; // Vanilla also checks for the graphics device
        }
        
        public bool IsLoaded => _textureTask is { IsValueCreated: true, Value.IsCompletedSuccessfully: true };

        public readonly ModAsset? Metadata;

        private readonly TextureKind _textureKind;
        private readonly object _textureLock;
        private CancellationTokenSource _cts;
        private TextureLoader.IPreLoader _preLoader;
        private LTT _textureTask; // This is the new Texture_Unsafe
        private Texture2D? _cachedTexture; // Fast way to access a texture, to avoid pointer chasing
        private bool _disposed;

        [MonoModConstructor]
        [MonoModReplace]
        internal patch_VirtualTexture(string path) {
            ArgumentException.ThrowIfNullOrEmpty(path);
            _textureKind = TextureKind.FileSystem;
            Path = path;
            Name = path;
            _textureLock = new object();
            _preLoader = CreatePreLoader();
            _cts = new CancellationTokenSource();
            _textureTask = null!;
            InitializeTexture();
        }

        [MonoModConstructor]
        [MonoModReplace]
        internal patch_VirtualTexture(string name, int width, int height, Color color) {
            ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
            _textureKind = TextureKind.SizeDefined;
            Name = name;
            _width = width;
            _height = height;
            this.color = color;
            _textureLock = new object();
            _preLoader = CreatePreLoader();
            _cts = new CancellationTokenSource();
            _textureTask = null!;
            InitializeTexture();
        }

        [MonoModConstructor]
        internal patch_VirtualTexture(ModAsset metadata) {
            ArgumentNullException.ThrowIfNull(metadata);
            _textureKind = TextureKind.ModAsset;
            Metadata = metadata;
            Name = metadata.PathVirtual;
            _textureLock = new object();
            _preLoader = CreatePreLoader();
            _cts = new CancellationTokenSource();
            _textureTask = null!;
            InitializeTexture();
        }
        
        /// <summary>
        /// Causes a reload (or just load) of the texture, it may complete asynchronously.
        /// </summary>
        [MonoModReplace]
        internal override sealed void Reload() {
            lock (_textureLock) {
                CancelLoadUnlocked(); // Canceling is required because it disposes the texture if it got loaded
                // We need to reload the preloader too, in case there were any changes affecting it
                _preLoader = CreatePreLoader();
                InitializeTexture(false);
            }
        }
        
        /// <summary>
        /// Unloads the texture from video memory.
        /// </summary>
        [MonoModReplace]
        internal override void Unload() {
            lock (_textureLock) {
                CancelLoadUnlocked(); // Canceling is required because it disposes the texture if it got loaded
                // No need to regenerate the preloader since Unload does not need to refresh data
                InitializeTexture(true);
            }
        }

        /// <summary>
        /// Disposes the native resources and unregisters itself.
        /// </summary>
        [MonoModReplace]
        public override void Dispose() {
            // Disposing is weird in vanilla, it only unloads the texture and unregisters from VirtualContent
            // here we keep this behavior and try to make IsDisposed monotone rather than being just a cheker
            // for whether the texture is available and not disposed
            Unload();
            Volatile.Write(ref _disposed, true);
            patch_VirtualContent.Remove(this);
        }
        
        private TextureLoader.IPreLoader CreatePreLoader() {
            switch (_textureKind) {
                case TextureKind.FileSystem: {
                    Debug.Assert(Path is not null);
                    return System.IO.Path.GetExtension(Path) switch {
                        ".data" => new DataTextureLoader.DataPreLoader(StreamProviderFS),
                        ".png" => new PNGTextureLoader.PNGPreLoader(StreamProviderFS, Path),
                        ".xnb" => new XnbTextureLoader.XnbPreLoader(Path),
                        _ => new FallbackTextureLoader.FallbackPreLoader(StreamProviderFS, false)
                    };
                    break;

                    Stream StreamProviderFS(bool actualLoad) {
                        return new FileStream(System.IO.Path.Combine(Engine.ContentDirectory, Path), 
                            FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                }
                case TextureKind.ModAsset: {
                    Debug.Assert(Metadata is not null);
                    // Old FTL code used to check if StreamProvider() == null, and if so assigned a fallback
                    // But this would have crashed on the old Preload function before this could happen so the
                    // new impl omits this check and assumes that it doesn't ever happen.
                    Debug.Assert(StreamProviderModAsset(false) is not null);
                    if (Metadata.Format == "png") {
                        return new PNGTextureLoader.PNGPreLoader(StreamProviderModAsset, Name, !Metadata.StreamAsync);
                    } else {
                        bool premul = false; // Assume unpremultiplied by default
                        if (Metadata.TryGetMeta(out TextureMeta meta))
                            premul = meta.Premultiplied;
                        return new FallbackTextureLoader.FallbackPreLoader(StreamProviderModAsset, premul);
                    }
                    break;

                    Stream StreamProviderModAsset(bool actualLoad) {
                        Stream stream = Metadata.Stream;
                        if (actualLoad) { 
                            // Mod assets benefit from being copied into memory ahead of time to avoid lock contention
                            // in the hot paths of texture loading.
                            MemoryStream ms = new();
                            stream.CopyTo(ms);
                            stream.Dispose();
                            ms.Position = 0;
                            stream = ms;
                        }
                        return stream;
                    }
                }
                case TextureKind.SizeDefined: {
                    Debug.Assert(_width > 0 && _height > 0);
                    return new SizeDefinedTextureLoader.SizeDefinedPreLoader(_width, _height, color);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
        }
        
        // Extra setup common in all constructors
        [MemberNotNull(nameof(_textureTask))]
        private void InitializeTexture(bool? lazyOverride = null) {
            _cachedTexture = null;
            if (Everest.Flags.IsHeadless) {
                // On headless we always lazyload for performance reasons, so this has no use
                if (lazyOverride == null)
                    Everest.Events.VirtualTexture.OnShouldForceLazyLoad((VirtualTexture) (object) this);
                // If a preload is not possible just load the texture, even on headless,
                // otherwise we risk having the wrong size, skipping loads entirely is just a
                // performance optimization
                if (_orig_width > 0 && _orig_height > 0)
                    _textureTask = new LTT(Task.FromResult(new Texture2D(Engine.Graphics.GraphicsDevice, _orig_width, _orig_height)));
                else if (_preLoader.GetPreloadedSize() != null) {
                    Point preloadedSize = _preLoader.GetPreloadedSize()!.Value;
                    _textureTask = new LTT(Task.FromResult(new Texture2D(Engine.Graphics.GraphicsDevice, preloadedSize.X, preloadedSize.Y)));
                } else {
                    _textureTask = new LTT(CreateTask());
                }
            } else {
                // Try to lazily create the task eagerly, if there's no preload EnsurePublicFields will load it anyway
                // Don't call the event when there's an override
                bool doLazyLoad = lazyOverride ?? 
                                  Everest.Events.VirtualTexture.OnShouldForceLazyLoad((VirtualTexture) (object) this) 
                                  || CoreModule.Settings.LazyLoading;
                if (doLazyLoad) {
                    _textureTask = new LTT(CreateTask); // Pass a delegate to make loading lazy
                } else {
                    _textureTask = new LTT(CreateTask()); // Immediately call the loading routine to load immediately
                }
            }
            EnsurePublicFields();
        }

        // Makes sure that the non lazily loaded fields get initialized, blocking if needed
        private void EnsurePublicFields() {
            // Blocking is only needed on first load, this also allows for an "Unloaded" state
            if (_orig_width > 0 && _orig_height > 0) {
                _width = _orig_width;
                _height = _orig_height;
                return;
            }
            if (IsLoaded) { // If the texture is ready read from it to avoid unnecessary Preloads
                Texture2D tex = _textureTask.Value.Result;
                _width = tex.Width;
                _height = tex.Height;
            } else {
                Point? preLoadSize = _preLoader.GetPreloadedSize();
                if (preLoadSize != null) {
                    Point size = preLoadSize.Value;
                    _width = size.X;
                    _height = size.Y;
                } else {
                    // Never call _textureTask.Value.Result without knowing you're not on main thread or that its completed!
                    // Otherwise, we could deadlock!
                    Texture2D tex = SafeWaitForTextureUnlocked();
                    _width = tex.Width;
                    _height = tex.Height;
                }
            }
            _orig_width = _width;
            _orig_height = _height;
        }
        
        // Should always be in a lock
        private void CancelLoadUnlocked() {
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
            if (IsLoaded) { // Dispose any loaded results
                // Dispose should also be called on the main thread, there's no point in waiting since
                // it doesn't really matter if the texture is disposed now or later.
                MainThreadHelper.Schedule(_textureTask.Value.Result.Dispose);
            } else if (_textureTask is { IsValueCreated: true, Value.IsFaulted: true }) { // Or fetch any exceptions that may have occurred
                _ = _textureTask.Value.Result;
            }
        }

        // Waits for the texture to finish loading while ensuring no deadlocks occur due to stalling the main thread
        private Texture2D SafeWaitForTextureUnlocked() {
            Task<Texture2D> task = _textureTask.Value;
            // Extra handling code to prevent deadlocks if the main thread needs to wait for a result
            Debug.Assert(task != null);
            if (MainThreadHelper.IsMainThread && !task.IsCompleted) {
                if (MainThreadHelper.BusyWaitTask(TextureContentHelper.Pipeline.TryMoveToPriorityPipeline(task))) {
                    Logger.Verbose(nameof(VirtualTexture), "Prioritized task!");
                } else {
                    if (!task.IsCompleted) {
                        Logger.Verbose(nameof(VirtualTexture), "Couldn't prioritize task and it is not completed!");
                    }
                }
                // On main thread let's just run other tasks while we wait
                MainThreadHelper.BusyWaitTask(task);
            }
            return task.Result;
        }
        
        private Task<Texture2D> CreateTask() {
            return TextureContentHelper.CreateFTLTask(_preLoader, _cts.Token);
        }

        private enum TextureKind {
            FileSystem,
            ModAsset,
            SizeDefined
        }
    }

#nullable disable
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
        public static void SetFallback(this VirtualTexture self, VirtualTexture fallback) {
            //=> ((patch_VirtualTexture) (object) self).Fallback = fallback;
        }

    }
}

namespace Celeste.Mod {
    public static partial class Everest {
        public static partial class Events {
            public static class VirtualTexture {
                public delegate bool ForceLazyLoadHandler(Monocle.VirtualTexture self);

                public static event ForceLazyLoadHandler ShouldForceLazyLoad;

                internal static bool OnShouldForceLazyLoad(Monocle.VirtualTexture self) {
                    return ShouldForceLazyLoad.InvokeWhileFalse(self);
                }

                public delegate void LazyLoadHandler(Monocle.VirtualTexture self);

                public static event LazyLoadHandler OnLazyLoad;
                internal static void LazyLoad(Monocle.VirtualTexture tex)
                    => OnLazyLoad?.Invoke(tex);
            }
        }
    }
}