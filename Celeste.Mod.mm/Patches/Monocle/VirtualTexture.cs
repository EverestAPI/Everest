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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Monocle {
    class patch_VirtualTexture : patch_VirtualAsset {

        // We're effectively in VirtualAsset, but still need to "expose" private fields to our mod.
        public string? Path { get; private set; }
        private Color color;
        
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
                    Action queuedLoadAct;
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
                        // so just run it directly, we are on the main thread anyway
                        queuedLoadAct();
                    } else {
                        queuedLoad.Wait();
                    }
                } while (true);
            }
            set {
                // TODO: Add a flag to know if the texture was overriden, so that Reload is idempotent
                // It does not make much sense to assign to the texture, but some mods do, and vanilla allows for that to happen.
                // Un-synchronized assignments will often lead to race conditions, but there's not much we can do other than keep the state of this object valid.
                if (value == null) {
                    Unload();
                } else {
                    AssignTexture(value);
                }
            }
        }

        public readonly ModAsset? Metadata;

        public VirtualTexture? Fallback;

        private readonly object _textureLock;
        // Queued upload to gpu of the texture, assumed to always run on main thread
        private Task? _queuedLoad;
        private Action? _queuedLoadAct;
        private readonly object _reloadLock;
        private volatile bool _reloadInProgress;
        private bool isPreloaded;
        private Exception? asyncFault;

        public static bool FtlToggle { get; internal set; }

        [MonoModConstructor]
        [MonoModReplace]
        internal patch_VirtualTexture(string path) {
            Path = path;
            Name = path;
            _textureLock = new object();
            _reloadLock = new object();
            CtorLoad();
        }

        [MonoModConstructor]
        [MonoModReplace]
        internal patch_VirtualTexture(string name, int width, int height, Color color) {
            Name = name;
            Width = width;
            Height = height;
            this.color = color;
            _textureLock = new object();
            _reloadLock = new object();
            CtorLoad();
        }

        [MonoModConstructor]
        internal patch_VirtualTexture(ModAsset metadata) {
            Metadata = metadata;
            Name = metadata.PathVirtual;
            _textureLock = new object();
            _reloadLock = new object();
            CtorLoad();
        }

        private void CtorLoad() {
            if (Everest.Flags.IsHeadless) {
                Preload();
                Everest.Events.VirtualTexture.OnShouldForceLazyLoad((VirtualTexture) (object) this);
                AssignTexture(new Texture2D(Engine.Graphics.GraphicsDevice, 1, 1));
                return;
            }
            // Only skip reloads with lazyloading and successful preloads
            if (!Preload() || (!Everest.Events.VirtualTexture.OnShouldForceLazyLoad((VirtualTexture) (object) this) && !CoreModule.Settings.LazyLoading))
                Reload();
        }


        /// <summary>
        /// Runs a callback in the main thread.
        /// </summary>
        /// <param name="cb">The callback.</param>
        /// <param name="wait">Whether to wait.</param>
        private void RunSafely(Func<Texture2D> cb, bool wait = false) {
            if (LoadImmediately) {
                AssignTexture(cb());
                return;
            }

            bool hasRunQueuedLoad = false;
            Action act = RunAndStore;
            ValueTask vt = MainThreadHelper.Schedule(act);
            // This is somewhat pointless, IsCompleted cant be true with the current LoadImmediately criteria
            if (vt.IsCompleted) { // TODO: What if the completion was not successful
                return;
            }

            Task t = vt.AsTask();
            lock (_textureLock) {
                _queuedLoad = t;
                _queuedLoadAct = act;
            }
            if (wait) {
                t.Wait();
            }
            
            return;

            void RunAndStore() {
                if (hasRunQueuedLoad) return;
                hasRunQueuedLoad = true;
                Texture2D tex = cb();
                AssignTexture(tex);
                // This callback may hold references to big memory arrays, so cut the references once we are done
                lock (_textureLock) {
                    _queuedLoad = null;
                    _queuedLoadAct = null;
                }
            }
        }

        // Make sure that MainThreadHelper.IsMainThread == true implies LoadImmediately == true
        private bool LoadImmediately => MainThreadHelper.IsMainThread;

        // This function assumes it is executing on at most one thread at a time
        private void InnerReload(bool block = false) {
            asyncFault = null;
            Unload();
            // Important, this is the main entrypoint, and any code-path should lead to an eventual unique call to RunSafely or AssignTexture
            int preW = -1;
            int preH = -1;
            if (isPreloaded) {
                preW = Width;
                preH = Height;
            }
        
            if (Metadata != null) {
                Stream stream = Metadata.Stream;
                if (stream != null) {
                    bool premul = false; // Assume unpremultiplied by default.
                    if (Metadata.TryGetMeta(out TextureMeta meta))
                        premul = meta.Premultiplied;
                    // If we have async streams, read async, otherwise, wrap everything in the RunSafely call and run the returned cb immediately
                    // TODO: This is a bit wasteful, especially if we moved out of main thread to load asynchronously, maybe check asynchronousness beforehand?
                    RunSafely(Metadata.StreamAsync ?
                        TextureContentHelper.LoadFromStream(stream, premul, preW, preH) : 
                        () => TextureContentHelper.LoadFromStream(stream, premul, preW, preH)(), block);
                } else if (Fallback != null) {
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    ((patch_VirtualTexture) (object) Fallback).Reload();
                    AssignTexture(Fallback.Texture!);
                } else {
                    throw new InvalidOperationException();
                }
            } else if (string.IsNullOrEmpty(Path)) {
                RunSafely(TextureContentHelper.LoadFromSizeAndColor(Width, Height, color), block);
            } else {
                RunSafely(TextureContentHelper.LoadFromPath(Path, preW, preH), block);
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
                    // Logger.Log(LogLevel.Info, nameof(VirtualTexture), "Offloading texture: " + (Path ?? Name ?? $"{Width}x{Height}"));
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
                Logger.Error(nameof(VirtualTexture), $"Failed loading texture {Name ?? $"{Width}x{Height}"}!");
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

        private void AssignTexture(Texture2D tex) {
            ArgumentNullException.ThrowIfNull(tex);
            lock (_textureLock) {
                // if (Texture_Unsafe != null) {
                //     throw new InvalidOperationException("Double Texture_Unsafe assignment!");
                // }
                Texture_Unsafe = tex;
                Width = tex.Width;
                Height = tex.Height;
            }
        }
        
        // TODO: Get rid of the replace and ilpatch to use Texture_Unsafe
        [MonoModReplace]
        internal override void Unload() {
            lock (_textureLock) {
                if (Texture_Unsafe is { IsDisposed: false }) {
                    Texture_Unsafe.Dispose();
                }
                Texture_Unsafe = null;
            }
        }

        // TODO: Get rid of the replace and ilpatch to use Texture_Unsafe
        [MonoModReplace]
        public override void Dispose() {
            Unload();
            // Texture_Unsafe = null;
            patch_VirtualContent.Remove(this);
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
                    return isPreloaded = true;

                } else if (extension == ".png") {
                    // Hard.
                    using (FileStream stream = File.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
                        return isPreloaded = PreloadSizeFromPNG(stream, Path);

                } else {
                    // .xnb and other file formats - impossible.
                    return false;

                }

            } else if (Metadata != null) {
                if (Metadata.Format == "png") {
                    // Hard.
                    using (Stream stream = Metadata.Stream)
                        return isPreloaded = PreloadSizeFromPNG(stream, $"{Metadata.PathVirtual} (mod {Metadata.Source.Mod?.Name ?? "*unknown*"})");

                } else {
                    // .xnb and other file formats - impossible.
                    return false;
                }
            }

            // If we have nothing to work with but the size is already there just roll with it
            // this often happens with textures that use the width-height-color ctor
            if (Width != 0 && Height != 0) {
                return isPreloaded = true;
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
