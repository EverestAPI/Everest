using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#nullable enable

namespace Celeste.Mod.Helpers;

public static class TextureContentHelper {
    [ThreadStatic]
    private static byte[]? bytes;
    private const int bytesSize = 512 * 1024; // 524288
    private const int bytesCheckSize = 512 * 1024 - 32; // 524256
    private const int atlasSize = 4096 * 4096 * 4;
    private static bool inGC = true;
    private static readonly SpanPool<byte> spanPool = new(atlasSize * 2, inGC);
    
    public static unsafe Func<Texture2D> LoadFromPath(string path) {
        int w, h;
        switch (Path.GetExtension(path)) {
            case ".data":
                bool hasSegment;
                SpanPool<byte>.SegmentIdentifier seg;
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
                    hasSegment = spanPool.TryRent(size, out seg);
                    Span<byte> buffer;
                    if (hasSegment) {
                        buffer = seg.Memory.Span;
                    } else {
                        // Fall back to gc allocs
                        // TODO: Improve this to bound max mem usage
                        buffer = new byte[size];
                    }

                    if (hasAlpha) {
                        ReadDataFile<HasAlpha>(stream, read, buffer);
                    } else {
                        ReadDataFile<NoAlpha>(stream, read, buffer);
                    }
                }
                return () => {
                    Texture2D tex = new(Celeste.Instance.GraphicsDevice, w, h);
                    fixed (byte* ptr = seg.Memory.Span)
                        tex.SetData((IntPtr)ptr);
                    
                    if (hasSegment)
                        spanPool.Return(seg);
                    return tex;
                };
            case ".png":
                return LoadFromStream(File.OpenRead(Path.Combine(Engine.ContentDirectory, path)), false /* pngs are never premultiplied */);
            case ".xnb":
                return () => Engine.Instance.Content.Load<Texture2D>(path.Replace(".xnb", ""));
            default:
                return () => {
                    return LoadFromStream(File.OpenRead(Path.Combine(Engine.ContentDirectory, path)), false)();
                    using FileStream stream = File.OpenRead(Path.Combine(Engine.ContentDirectory, path));
                    return Texture2D.FromStream(Engine.Graphics.GraphicsDevice, stream);
                };
        }
    }
    
    // stb path
    public static Func<Texture2D> LoadFromStream(Stream stream, bool premul) {
        using (stream) {
            int w, h;
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
                return tex;
            };
        }
    }
    
    public static unsafe Func<Texture2D> LoadFromSizeAndColor(int width, int height, Color color) {
        // Layout order for Color is unknown, but since it's guaranteed to be consistent everywhere this will work
        bool hasSegment = spanPool.TryRent(width * height * sizeof(Color), out SpanPool<byte>.SegmentIdentifier seg);
        Memory<byte> data = hasSegment ? seg.Memory :
            // TODO: Improve this to bound max mem usage
            new byte[width * height * sizeof(Color)];
        fixed (Color* ptr = MemoryMarshal.Cast<byte, Color>(data.Span)) {
            for (int i = 0; i < data.Length; i++) {
                ptr[i] = color;
            }
        }
        return () => {
            Texture2D tex = new(Engine.Instance.GraphicsDevice, width, height);
            fixed (byte* ptr = data.Span) {
                tex.SetData((IntPtr)ptr);
            }
            // Fall back to gc allocs
            // TODO: Improve this to bound max mem usage
            if (hasSegment)
                spanPool.Return(seg);
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

    private sealed class HasAlpha : AlphaMode;

    private sealed class NoAlpha : AlphaMode;

    private class SpanPool<T> : IDisposable where T : unmanaged {
        private readonly int Size;
        private readonly object @lock = new();

        private readonly ManagedOrNotArray<T> arrayHolder;
        private readonly Memory<T> array;
        private readonly List<(int start, int end)> usedSegments = new();
        
        public SpanPool(int itemCount, bool inGC) {
            Size = itemCount;
            arrayHolder = new ManagedOrNotArray<T>(itemCount, inGC);
            array = arrayHolder.AsMemory();
        }

        public bool TryRent(int size, out SegmentIdentifier seg) {
            lock (@lock) {
                (int start, int end)? freeSegment = NextFreeSegmentAndReserve(size);
                if (!freeSegment.HasValue) { // No space
                    seg = default;
                    return false;
                }
                
                // We have a spot
                Memory<T> memory = array[freeSegment.Value.start..freeSegment.Value.end];
                seg = new SegmentIdentifier(memory, freeSegment.Value.start, freeSegment.Value.end);
                return true;
            }
        }

        public void Return(SegmentIdentifier seg) {
            lock (@lock) {
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
            }
        }
        
        public void Dispose() {
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
            if (Size - prevIdx >= minSize) {
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
