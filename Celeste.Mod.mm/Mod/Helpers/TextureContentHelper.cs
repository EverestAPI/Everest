//#define TRACE_GC_ALLOCS

using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

#nullable enable

namespace Celeste.Mod.Helpers;

public static class TextureContentHelper {
    [ThreadStatic]
    private static byte[]? bytes;
    private const int bytesSize = 512 * 1024; // 524288
    private const int bytesCheckSize = 512 * 1024 - 32; // 524256
    public const int atlasSize = 4096 * 4096 * 4;
    internal static readonly FTLMemoryManger MemoryManager;
    static TextureContentHelper() {
        const bool inGC = true; // TODO: unhardcode
        const int initialMemUsage = atlasSize * 4; // TODO: unhardcode
        MemoryManager = new FTLMemoryManger(initialMemUsage, inGC);
    }

    public static bool TryEnableFTL() {
        /* Vanilla calls GFX.Load and MTN.Load in LoadContent on non-Stadia platforms.
         * Sadly we can't load them in GameLoader.LoadThread as mods rely on them in LoadContent.
         *
         * Loading in a new thread with texture -> GPU ops on the main thread helps barely.
         * Spawning a new thread just to wait for it to end doesn't make much sense,
         * BUT delaying the slow texture load ops to happen lazy-async gets the game window to appear sooner.
         *
         * Note that on XNA, this dies both with and without threaded GL due to OOM exceptions.
         * -ade
         */
        if (patch_VirtualTexture.FtlToggle) return true;
        if (CoreModule.Settings.FastTextureLoading ?? Environment.ProcessorCount >= 4) {
            long limit = (long) (CoreModule.Settings.FastTextureLoadingMaxMB * 1024f * 1024f);

            if (limit <= 0) {
                limit = (long) (Everest.SystemMemoryMB * 0.2f * 1024f * 1024f);
                // Assume that even in the worst case with 4 GB system RAM, 512 MB (= 12.5% = 1/8) are still available for texture loads.
                if (limit <= (512L * 1024L * 1024L))
                    limit = (512L * 1024L * 1024L);
            }
            // ... and even if the user forcibly lowered it below 128 MB, fall back to 128 MB as even the vanilla gameplay atlas is 64MB.
            if (limit <= (128L * 1024L * 1024L))
                limit = (128L * 1024L * 1024L);

            Logger.Info("LoadContent", $"Enabling FTL with {limit} bytes");
            patch_VirtualTexture.FtlToggle = true;
            MemoryManager.SetAllocSize(limit);
            return true;
        }
        return false;
    }
    
    public static Func<Texture2D> LoadFromPath(string path, int preW = -1, int preH = -1) {
        int w, h;
        switch (Path.GetExtension(path)) {
            case ".data":
                bool hasSegment;
                SpanPoolPool<byte>.SegmentIdentifier seg;
                Memory<byte> mem;
                using (FileStream stream = File.OpenRead(Path.Combine(Engine.ContentDirectory, path))) {
                    
                    // Vanilla has got a static readonly byte[] bytes of fixed length - currently 524288
                    // Luckily we can read more chunks on demand.
                    byte[] read = bytes ??= new byte[bytesSize];

                    _ = stream.Read(read, 0, bytesSize);
    
                    // Read the width, height and alpha mode
                    w = BitConverter.ToInt32(read, 0);
                    h = BitConverter.ToInt32(read, 4);
                    bool hasAlpha = read[8] == 1;
    
                    int size = w * h * 4;
                    {
                        hasSegment = MemoryManager.GetChunkOrGcAlloc(size, out seg, out byte[] gcArray
#if TRACE_GC_ALLOCS
                            , $"Path texture {path}"
#endif
                        );
                        mem = hasSegment ? seg.SegId.Memory : gcArray;
                    }

                    Span<byte> buffer = mem.Span;
                    if (hasAlpha) {
                        ReadDataFile<HasAlpha>(stream, read, buffer);
                    } else {
                        ReadDataFile<NoAlpha>(stream, read, buffer);
                    }
                }
                return () => {
                    Texture2D tex = new(Celeste.Instance.GraphicsDevice, w, h);
                    unsafe {
                        fixed (byte* ptr = mem.Span)
                            tex.SetData((IntPtr) ptr);
                    }
                    if (hasSegment)
                        MemoryManager.ReturnChunk(seg);
                    return tex;
                };
            case ".png":
                return LoadFromStream(File.OpenRead(Path.Combine(Engine.ContentDirectory, path)), false /* pngs are never premultiplied */, preW, preH);
            case ".xnb":
                // TODO: Are xnbs worth accelerating?
                return () => Engine.Instance.Content.Load<Texture2D>(path.Replace(".xnb", ""));
            default:
                return () => LoadFromStream(File.OpenRead(Path.Combine(Engine.ContentDirectory, path)), false)();
        }
    }
    
    public static Func<Texture2D> LoadFromStream(Stream stream, bool premul, int w = -1, int h = -1) {
        using (stream) {
            // This code will ultimately use stb_image to decode whatever is in stream
            // we cannot control its allocation, so estimate it based on the image size
            // and some arbitrary inflation coefficient
            const double inflationCoef = 1.2;
            long unmanagedClaimed = 0;
            if (w > 0 && h > 0) {
                unmanagedClaimed = (long) ((double) w * h * 4 * inflationCoef);
                // ClaimUnmanaged gets us the amount that we managed to claim
                unmanagedClaimed = MemoryManager.ClaimUnmanaged(unmanagedClaimed
#if TRACE_GC_ALLOCS
                , $"Path texture {
                    stream switch {
                        FileStream fs => fs.Name,
                        SynchronizedZipEntryStream szes => szes.entry.FullName,
                        _ => "Unknown path"
                    }
                }"
#endif
                );
            }
            // If we don't know the size beforehand VirtualTexture is in charge of not multithreading loads
            IntPtr dataPtr; // assume Texture.SetData supports Ptr since we are using FNA
            if (premul)
                ContentExtensions.LoadTextureRaw(Celeste.Instance.GraphicsDevice, stream, out w, out h, out dataPtr);
            else
                ContentExtensions.LoadTextureLazyPremultiply(Celeste.Instance.GraphicsDevice, stream, out w, out h, out dataPtr);
            stream.Dispose();
            return () => {
                Texture2D tex = new(Celeste.Instance.GraphicsDevice, w, h);
                tex.SetData(dataPtr);
                ContentExtensions.UnloadTextureRaw(dataPtr);
                if (unmanagedClaimed != 0)
                    MemoryManager.ReturnUnmanaged(unmanagedClaimed);
                return tex;
            };
        }
    }
    
    public static Func<Texture2D> LoadFromSizeAndColor(int width, int height, Color color) {
        // Layout order for Color is unknown, but since it's guaranteed to be consistent everywhere this will work
        bool hasSegment = MemoryManager.GetChunkOrGcAlloc(width*height*Unsafe.SizeOf<Color>(), 
            out SpanPoolPool<byte>.SegmentIdentifier seg, out byte[] gcArray
#if TRACE_GC_ALLOCS
            , $"Sized texture {width}x{height}"
#endif
            );
        Memory<byte> data = hasSegment ? seg.SegId.Memory : gcArray;
        Span<Color> colorData = MemoryMarshal.Cast<byte, Color>(data.Span);
        colorData.Fill(color);
        return () => {
            Texture2D tex = new(Engine.Instance.GraphicsDevice, width, height);
            unsafe {
                fixed (byte* ptr = data.Span) {
                    tex.SetData((IntPtr)ptr);
                }
            }
            // Fall back to gc allocs
            // TODO: Improve this to bound max mem usage
            if (hasSegment)
                MemoryManager.ReturnChunk(seg);
            return tex;
        };
    }

    // Abuse generics in order to get dead code elimination for optimal code on both cases
    // This method simply reads from the `read` array and decodes to the `buffer` span,
    // It also expects `read` to be prefilled with the first part of `stream` and it 
    // will keep reading from `stream` until all data is decoded.
    // Assumptions: read.Length >= bytesCheckSize, stream.Position == read.Length
    [MethodImpl(MethodImplOptions.AggressiveInlining)] // This used to be inlined manually
    private static unsafe void ReadDataFile<T>(Stream stream, byte[] read, Span<byte> buffer) where T : AlphaMode {
        int size = buffer.Length;
        fixed (byte* to = buffer)
        fixed (byte* from = read) {
            int* toI = (int*) to;
            uint toIdxB = 0;
            uint toIdxI = 0;
            int readIdx = 9; // the first 9 bytes describe width, height and alpha mode (4+4+1), those have been read already
            while (toIdxB < size) {
                // Pixel values are run length encoded, this counts the number of pixels in this line
                uint lineSize = from[readIdx];

                bool zeroSplat = false;
                if (typeof(T) == typeof(HasAlpha)) {
                    // If there is a nonzero alpha, all 4 bytes are stored, if alpha is zero, a single byte is
                    byte a = from[readIdx + 1];
                    if (a > 0) {
                        to[toIdxB] = from[readIdx + 4];
                        to[toIdxB + 1] = from[readIdx + 3];
                        to[toIdxB + 2] = from[readIdx + 2];
                        to[toIdxB + 3] = a;
                        readIdx += 1 + 4;
                    } else {
                        toI[toIdxI] = 0;
                        readIdx += 1 + 1;
                        zeroSplat = true;
                    }
                } else {
                    to[toIdxB] = from[readIdx + 3];
                    to[toIdxB + 1] = from[readIdx + 2];
                    to[toIdxB + 2] = from[readIdx + 1];
                    to[toIdxB + 3] = 255;
                    readIdx += 4;
                }

                if (lineSize > 1) {
                    if (typeof(T) == typeof(HasAlpha) && zeroSplat) {
                        // If alpha was zero, bulk write 0 to the whole line
                        Unsafe.InitBlockUnaligned(to + toIdxB + 4, 0, lineSize * 4 - 4);
                    } else {
                        // Write via integers for performance
                        int splatValue = toI[toIdxI];
                        for (uint jI = toIdxI + 1, end = toIdxI + lineSize; jI < end; jI++)
                            toI[jI] = splatValue;
                    }
                }

                // Advance
                toIdxI += lineSize;
                toIdxB = toIdxI * 4;

                // If there is less than 32 bytes left, copy the remaining ones to the beginning and read from the stream again
                if (readIdx > bytesCheckSize) {
                    int offset = read.Length - readIdx;
                    for (int oB = 0; oB < offset; oB++) {
                        from[oB] = from[readIdx + oB];
                    }
                    _ = stream.Read(read, offset, read.Length - offset);
                    readIdx = 0;
                }
            }
        }
    }

    private interface AlphaMode;

    private struct HasAlpha : AlphaMode {
        // The structs need to be of different sizes in order to force the JIT to compile two different versions
        private int _;
    };

    private struct NoAlpha : AlphaMode;

    internal class FTLMemoryManger(long initialMemUsage, bool inGC) {
        private const double SplitPercent = 1 / 4D;
        private readonly long initialMemUsage = initialMemUsage;
        private const int MainThreadTimeout = 500;
        private const int OtherThreadTimeout = -1;
        // Managed
        private readonly ResourceWaiter waitingForSpace = new(MainThreadTimeout, OtherThreadTimeout);
        private readonly SpanPoolPool<byte> spanPool = new(atlasSize, (long)(initialMemUsage*SplitPercent), inGC);
        
        // Unmanaged
        private long unmanagedMemoryUsage = 0;
        private readonly object unmanagedLock = new();
        private readonly ResourceWaiter unmanagedWaitingForSpace = new(MainThreadTimeout, OtherThreadTimeout);
        
        public long CurrMemUsage { get; private set; } = initialMemUsage;
        
        public bool GetChunkOrGcAlloc(int chunkSize, out SpanPoolPool<byte>.SegmentIdentifier seg, out byte[] gcArray
#if TRACE_GC_ALLOCS
        , string source
#endif
        ) {
            // The cts may be swapped at any point, so if the available space decreases after TryRent fails and a new cts
            // is assigned before this gets to the Wait it would be waiting to never have space anyway (for now)
            if (chunkSize > atlasSize) { // It's not going to fit
                // Just gc alloc it, TODO: what should we do here?
                Logger.Warn(nameof(TextureContentHelper), $"Chunk size was too big for the current allocated memory ({chunkSize} > {spanPool.CurrMemUsage})!");
#if TRACE_GC_ALLOCS
                Logger.Warn(nameof(TextureContentHelper), $"For texture {source}:");
                Logger.Warn(nameof(TextureContentHelper), new StackTrace().ToString());
#endif
                gcArray =  new byte[chunkSize];
                seg = default;
                return false;
            }
            
            while (true) {
                bool hasSegment = spanPool.TryRent(chunkSize, out seg);
                if (hasSegment) {
                    gcArray = [];
                    return true;
                }

                if (!waitingForSpace.Wait()) // On timeout just exit and gc alloc
                    break;
            }
        
            bool isMainThread = MainThreadHelper.IsMainThread;
            Logger.Warn(nameof(TextureContentHelper), $"Allocating {chunkSize} bytes in the gc because " +
                                                                    $"{(isMainThread ? "the main-thread" : "a worker thread")} was " +
                                                                    $"blocked for more than {(isMainThread ? MainThreadTimeout : OtherThreadTimeout)}ms");
#if TRACE_GC_ALLOCS
            Logger.Warn(nameof(TextureContentHelper), $"For texture {source}:");
            Logger.Warn(nameof(TextureContentHelper), new StackTrace().ToString());
#endif
            gcArray = new byte[chunkSize];
            seg = default;
            return false;
        }

        public void ReturnChunk(SpanPoolPool<byte>.SegmentIdentifier seg) {
            spanPool.Return(seg);
            waitingForSpace.Pulse();
        }

        public long ClaimUnmanaged(long amount
#if TRACE_GC_ALLOCS
        , string source
#endif
        ) {
            while (true) {
                lock (unmanagedLock) {
                    if (unmanagedMemoryUsage + amount <= CurrMemUsage * (1 - SplitPercent)) {
                        unmanagedMemoryUsage += amount;
                        return amount;
                    }
                }
                if (!unmanagedWaitingForSpace.Wait()) { // On timeout just allocate without budget
                    bool isMainThread = MainThreadHelper.IsMainThread;
                    Logger.Warn(nameof(TextureContentHelper), $"Allocating {amount} bytes over the unmanaged budget because" +
                                                              $"{(isMainThread ? "the main-thread" : "a worker thread")} was " +
                                                              $"blocked for more than {(isMainThread ? MainThreadTimeout : OtherThreadTimeout)}ms");
#if TRACE_GC_ALLOCS
                    Logger.Warn(nameof(TextureContentHelper), $"For texture {source}:");
                    Logger.Warn(nameof(TextureContentHelper), new StackTrace().ToString());
#endif
                    break;
                }
            }
            return 0;
        }

        public void ReturnUnmanaged(long amount) {
            lock (unmanagedLock) {
                unmanagedMemoryUsage -= amount;
                Debug.Assert(unmanagedMemoryUsage >= 0);
            }
            unmanagedWaitingForSpace.Pulse();
        }

        public void SetAllocSize(long limit) {
            long prevMemUsage = CurrMemUsage;
            CurrMemUsage = limit == -1 ? initialMemUsage : limit;
            spanPool.CurrMemUsage = (long) (CurrMemUsage * SplitPercent);
            if (prevMemUsage < CurrMemUsage) {
                // Make everyone waiting recheck
                waitingForSpace.Pulse();
                unmanagedWaitingForSpace.Pulse();
            }
        }

        private class ResourceWaiter(int MainThreadTimeout, int OtherThreadTimeout) {
            private readonly ManualResetEventSlim mre = new();

            public bool Wait() {
                bool isMainThread = MainThreadHelper.IsMainThread;
                // TODO: this is sort of ugly, all threads should attempt to claim memory but we have no guarantee of that
                // What about our own impl of a better version?
                if (mre.Wait(isMainThread ? MainThreadTimeout : OtherThreadTimeout)) {
                    mre.Reset();
                    return true;
                }
                return false;
            }

            public void Pulse() {
                mre.Set();
            }
        }
    }

    // This class is thread-safe
    internal class SpanPoolPool<T> where T : unmanaged {
        private readonly object @lock = new();
        private readonly int size;
        private long currMemUsage;
        private readonly Dictionary<int, SpanPool<T>> pools = [];
        private int poolId = 0;
        private long releasePending = 0;
        private readonly bool _inGC;

        public long CurrMemUsage {
            get => Volatile.Read(ref currMemUsage);
            set {
                lock (@lock) {
                    currMemUsage = value;
                    CheckAlloc();
                }
            }
        }

        public SpanPoolPool(int poolSize, long initialMemUsage, bool gc) {
            size = poolSize;
            currMemUsage = initialMemUsage;
            _inGC = gc;
            CheckAlloc();
        }

        // Must be called from a lock
        private void CheckAlloc() {
            releasePending = 0;
            long minPoolCount = currMemUsage / size + (currMemUsage % size != 0 ? 1 : 0);
            if (pools.Count < minPoolCount) {
                for (int i = pools.Count; i < minPoolCount; i++) {
                    pools.Add(poolId++, new SpanPool<T>(size, _inGC));
                }
            } else if (pools.Count > minPoolCount) {
                int count = pools.Count;
                List<int> deallocating = [];
                foreach ((int id, SpanPool<T> pool) in pools) {
                    if (!pool.IsEmpty()) continue;
                    deallocating.Add(id);
                    count--;
                    if (count == minPoolCount) break;
                }
                foreach (int id in deallocating) {
                    pools[id].Dispose();
                    pools.Remove(id);
                }
                releasePending = count - minPoolCount;
            }
        }

        public bool TryRent(int chunkSize, out SegmentIdentifier segment) {
            lock (@lock) {
                for (int i = 0; i < 2; i++) {
                    // First try non-empty pools, then try empty ones
                    foreach ((int id, SpanPool<T> pool) in pools) {
                        if (pool.IsEmpty() == (i == 0)) continue;
                        bool hasSegment = pool.TryRent(chunkSize, out SpanPool<T>.SegmentIdentifier seg);
                        if (!hasSegment) continue;
                        segment = new SegmentIdentifier(seg, id);
                        return true;
                    }
                }
                
                // There's no space anywhere
                segment = default;
                return false;
            }
        }

        public void Return(SegmentIdentifier segment) {
            lock (@lock) {
                // Just return it to the correct pool
                if (!pools.TryGetValue(segment.PoolId, out SpanPool<T>? pool)) {
                    throw new ArgumentException($"Unknown pool with id {segment.PoolId}", nameof(segment));
                }
                pool.Return(segment.SegId);
                // But if we are looking to deallocate more, do so now
                if (pool.IsEmpty() && releasePending > 0) {
                    pool.Dispose();
                    pools.Remove(segment.PoolId);
                    releasePending--;
                }
            }
        }

        public readonly struct SegmentIdentifier(SpanPool<T>.SegmentIdentifier segmentIdentifier, int poolId) {
            public readonly SpanPool<T>.SegmentIdentifier SegId = segmentIdentifier;
            public readonly int PoolId = poolId;
        }
    }

    internal class SpanPool<T> : IDisposable where T : unmanaged {
        private readonly int size;
        // private readonly object @lock = new();

        private readonly ManagedOrNotArray<T> arrayHolder;
        private readonly Memory<T> array;
        private readonly List<(int start, int end)> usedSegments = new();
        
        public SpanPool(int itemCount, bool inGC) {
            size = itemCount;
            arrayHolder = new ManagedOrNotArray<T>(itemCount, inGC);
            array = arrayHolder.AsMemory();
        }

        public bool TryRent(int chunkSize, out SegmentIdentifier seg) {
            // lock (@lock) {
                (int start, int end)? freeSegment = NextFreeSegmentAndReserve(chunkSize);
                if (!freeSegment.HasValue) { // No space
                    seg = default;
                    return false;
                }
                
                // We have a spot
                Memory<T> memory = array[freeSegment.Value.start..freeSegment.Value.end];
                seg = new SegmentIdentifier(memory, freeSegment.Value.start, freeSegment.Value.end);
                return true;
            // }
        }

        public void Return(SegmentIdentifier seg) {
            // lock (@lock) {
                // Find the matching segment and remove it
                for (int i = 0; i < usedSegments.Count; i++) {
                    if (seg.Start == usedSegments[i].start) {
                        // Some extra verification to prevent corruption
                        if (seg.End != usedSegments[i].end) {
                            throw new ArgumentException("Invalid segment!");
                        }
                        usedSegments.RemoveAt(i);
                        break;
                    }
                }
            // }
        }
        
        public bool IsEmpty() => usedSegments.Count == 0;
        
        public void Dispose() {
            if (!IsEmpty()) {
                throw new Exception("Attempted to deallocate with segments potentially in use!");
            }
            arrayHolder.Dispose();
        }

        // Should always be called in a lock
        private (int, int)? NextFreeSegmentAndReserve(int minSize) {
            int prevIdx = 0;
            for (int i = 0; i < usedSegments.Count; i++) {
                int currIdx = usedSegments[i].Item1;
                if (currIdx - prevIdx >= minSize) { // Found a spot
                    (int, int) newSegment = (prevIdx, prevIdx+minSize);
                    usedSegments.Insert(i, newSegment);
                    return newSegment;
                }
                prevIdx = usedSegments[i].Item2;
            }
            // No in-between segments, check remaining space
            if (size - prevIdx >= minSize) {
                (int, int) newSegment = (prevIdx, prevIdx + minSize);
                usedSegments.Add(newSegment);
                return newSegment;
            }
            // No space
            return null;
        }

        public readonly struct SegmentIdentifier(Memory<T> memory, int start, int end) {
            public readonly Memory<T> Memory = memory;
            public readonly int Start = start;
            public readonly int End = end;
        }
    }

    private sealed class ManagedOrNotArray<T> : IDisposable where T : unmanaged {
        private readonly bool Managed;
        private readonly T[]? gcBuffer;
        private readonly UnmanagedMemoryManager<T>? ptrBuffer;
        
        public ManagedOrNotArray(int itemCount, bool managed) {
            Managed = managed;
            if (managed) {
                gcBuffer = new T[itemCount];
            } else {
                ptrBuffer = new UnmanagedMemoryManager<T>(itemCount);
            }
        }

        public Memory<T> AsMemory() {
            return Managed ? 
                new Memory<T>(gcBuffer!) :
                // Can't be null because Managed is false
                ptrBuffer!.Memory;
        }
        
        public void Dispose() {
            ((IDisposable?)ptrBuffer)?.Dispose();
        }
    }

    /// <summary>
    /// A simple MemoryManager for unmanaged memory arrays.
    /// </summary>
    /// <param name="itemCount">Number of elements of the array.</param>
    /// <typeparam name="T">Type of the array.</typeparam>
    private sealed unsafe class UnmanagedMemoryManager<T>(int itemCount) : MemoryManager<T>
        where T : unmanaged {
        private readonly T* ptr = (T*)Marshal.AllocHGlobal(itemCount*Marshal.SizeOf<T>());
        private bool disposed;

        protected override void Dispose(bool disposing) {
            if (disposed) throw new InvalidOperationException("Double free!");
            Marshal.FreeHGlobal((IntPtr)ptr);
            disposed = true;
        }
        
        public override Span<T> GetSpan() {
            return new Span<T>(ptr, itemCount);
        }
        
        public override MemoryHandle Pin(int elementIndex = 0) {
            // ptr won't ever move because it's not managed, so no pin needed.
            return new MemoryHandle(ptr + elementIndex);
        }
        
        public override void Unpin() {
            // No work needed, pinning doesnt happen
        }
    }
}
