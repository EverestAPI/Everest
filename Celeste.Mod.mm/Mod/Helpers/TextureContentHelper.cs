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
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

#nullable enable

namespace Celeste.Mod.Helpers;

// Class that does the heavy lifting of loading the texture to gpu memory in two stages
public abstract class TextureLoader : IDisposable {
    private IDisposable? memoryRent;
    protected readonly int rentSize;
    private Texture2DUploadable loadedData;

    protected TextureLoader(int preLoadedSize) {
        long rentSizeLong = preLoadedSize;
        // Add a lower bound for tiny textures
        if (rentSizeLong < 512*512) rentSizeLong = 512*512;
        // And a constant overhead for the loading procedure itself (especially IO)
        rentSizeLong += 1024 * 1024 / 4;
        // Just cap it for huge textures
        if (rentSizeLong > TextureContentHelper.MemoryManager.MaxMemoryUsageUnits)
            rentSizeLong = TextureContentHelper.MemoryManager.MaxMemoryUsageUnits;
        // And make sure this is a rentable amount
        if (rentSizeLong > int.MaxValue)
            rentSizeLong = int.MaxValue;
        rentSize = (int) rentSizeLong;
    }

    public async ValueTask StartLoad(CancellationToken ct) {
        if (rentSize >= 0) {
            memoryRent = await TextureContentHelper.MemoryManager.Rent(rentSize, ct);
        } else {
            // Getting here without knowing a size means we should have already be running in a synchronous manner.
            // Which means it should be okay to continue without claiming space
            Debug.Assert(ct == CancellationToken.None); // When preloads are not possible, we should be running synchronously
        }
        loadedData = ProcessDataAndPrepareUpload();
        ct.ThrowIfCancellationRequested();
    }
    
    // Decodes and loads the texture data into a cpu buffer that should be kept internally
    protected abstract Texture2DUploadable ProcessDataAndPrepareUpload();

    // Uploads the texture from the cpu buffer to gpu memory, should always run in the main thread
    public virtual Texture2D UploadTexture() {
        Texture2D tex = new(Celeste.Instance.GraphicsDevice, loadedData.W, loadedData.H);
        using MemoryHandle handle = loadedData.Data.Memory.Pin();
        unsafe {
            tex.SetData((IntPtr)handle.Pointer);
        }
        return tex;
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        loadedData.Dispose();
        memoryRent?.Dispose();
    }

    ~TextureLoader() {
        Dispose(false);
    }

    protected readonly struct Texture2DUploadable(IMemoryOwner<Color> data, int w, int h) : IDisposable {
        public readonly IMemoryOwner<Color> Data = data;
        public readonly int W = w;
        public readonly int H = h;

        public void Dispose() {
            Data.Dispose();
        }
    }
    
    // Helper class to handle preloads and create the TextureLoader instances
    public interface IPreLoader {
        // Common delegate for preloaders that use a stream, to be able to obtain one on demand.
        // `actualLoad` indicates whether the stream will be used for loading the texture or not.
        public delegate Stream StreamProvider(bool actualLoad);
        /// <summary>
        /// Tries to estimate the texture dimensions ahead of time.
        /// </summary>
        /// <returns>The estimated dimensions, or null.</returns>
        public Point? GetPreloadedSize();

        /// <summary>
        /// Creates a TextureLoader that will execute the loading process.
        /// </summary>
        /// <returns>The TextureLoader.</returns>
        public TextureLoader CreateLoader();
    }
}

// Loads textures by having fna do the heavy lifting of decoding
public abstract class FNAStreamTextureLoader : TextureLoader {
    private readonly Stream stream;
    private readonly bool preMul;

    // This code will ultimately use stb_image to decode whatever is in stream
    // we cannot control its allocation, so estimate it based on the image size
    // and some arbitrary inflation coefficient
    private const double inflationCoef = 1.2;
    protected FNAStreamTextureLoader(Stream stream, bool preMultiplied, int width = -1, int height = -1) 
        : base(width < 0 || height < 0 ? -1 : (int) (width * height * inflationCoef)) {
        this.stream = stream;
        preMul = preMultiplied;
    }

    protected override Texture2DUploadable ProcessDataAndPrepareUpload() {
        int w, h;
        IntPtr dataPtr;
        if (preMul)
            ContentExtensions.LoadTextureRaw(Celeste.Instance.GraphicsDevice, stream, out w, out h, out dataPtr);
        else
            ContentExtensions.LoadTextureLazyPremultiply(Celeste.Instance.GraphicsDevice, stream, out w, out h, out dataPtr);
        stream.Dispose();
        return new Texture2DUploadable(new UnmanagedMemoryManager(dataPtr, w*h*4), w, h);
    }
    
    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if (disposing) {
            stream.Dispose();
        }
    }

    private class UnmanagedMemoryManager(IntPtr dataPtr, int size) : MemoryManager<Color> {
        protected override void Dispose(bool disposing) {
            if (dataPtr != IntPtr.Zero) 
                ContentExtensions.UnloadTextureRaw(dataPtr);
        }
        
        public override unsafe Span<Color> GetSpan() {
            return new Span<Color>((void*)dataPtr, size);
        }
        
        public override unsafe MemoryHandle Pin(int elementIndex = 0) {
            return new MemoryHandle((void*) dataPtr);
        }
        
        public override void Unpin() {
        }
    }
}

// Loads PNG textures with preloading support
public sealed class PNGTextureLoader : FNAStreamTextureLoader {
    private PNGTextureLoader(Stream stream, int width, int height) : base(stream, false /* pngs are never premultiplied */, width, height) {
    }
    
    public class PNGPreLoader : IPreLoader {
        private readonly IPreLoader.StreamProvider streamProvider;
        private readonly string path;
        private int preW = -1;
        private int preH = -1;
        private bool preloaded;

        // We use stream providers because we open the stream multiple times
        public PNGPreLoader(IPreLoader.StreamProvider streamProvider, string path, bool noPreload = false) {
            this.streamProvider = streamProvider;
            if (noPreload) preloaded = true;
            this.path = path;
        }
        
        public Point? GetPreloadedSize() {
            DoPreload();
            return preW != -1 && preH != -1 ? new Point(preW, preH) : null;
        }

        private void DoPreload() {
            if (preloaded) return;
            preloaded = true;
            // Open the stream and dispose, seeking after preloading is much slower
            using Stream stream = streamProvider(false);
            bool preload = PreloadSizeFromPNG(stream, path, out int width, out int height);
            if (!preload) return;
            preW = width;
            preH = height;
        }

        public TextureLoader CreateLoader() {
            Point? preload = GetPreloadedSize();
            if (preload != null)
                return new PNGTextureLoader(streamProvider(true), preload.Value.X, preload.Value.Y);
            return new PNGTextureLoader(streamProvider(true), -1, -1);
        }
        
        private static bool PreloadSizeFromPNG(Stream stream, string path, out int width, out int height) {
            using BinaryReader reader = new(stream, Encoding.UTF8, true);
            ulong magic = reader.ReadUInt64();
            width = 0;
            height = 0;
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
            width = SwapEndian(reader.ReadInt32());
            height = SwapEndian(reader.ReadInt32());
            return true;
        }
    
        private static int SwapEndian(int data) {
            return
                ((data & 0xFF) << 24) |
                (((data >> 8) & 0xFF) << 16) |
                (((data >> 16) & 0xFF) << 8) |
                ((data >> 24) & 0xFF);
        }
    }
}

// Loads any kind of texture supported by FNA, without preload support
public sealed class FallbackTextureLoader : FNAStreamTextureLoader {
    private FallbackTextureLoader(Stream stream, bool preMul) : base(stream, preMul) {
    }

    public class FallbackPreLoader(IPreLoader.StreamProvider streamProvider, bool preMul) : IPreLoader {
        public Point? GetPreloadedSize() {
            return null;
        }
        
        public TextureLoader CreateLoader() {
            return new FallbackTextureLoader(streamProvider(true), preMul);
        }
    }
}

// Loads vanilla .data textures
public sealed class DataTextureLoader : TextureLoader {
    private const int bytesSize = 512 * 1024; // 524288
    private const int refillBufferMargin = 32; // 524256
    private readonly Stream stream;
    private static readonly ThreadLocal<byte[]> readArray = new(() => new byte[bytesSize]);

    private DataTextureLoader(Stream stream, int width, int height) : base(width * height) {
        this.stream = stream;
    }
    
    protected override Texture2DUploadable ProcessDataAndPrepareUpload() {
        Span<byte> whalpha = stackalloc byte[9];
        _ = stream.Read(whalpha);
        int w = BitConverter.ToInt32(whalpha);
        int h = BitConverter.ToInt32(whalpha[4..]);
        bool hasAlpha = whalpha[8] == 1;

        Memory<byte> memInput = readArray.Value.AsMemory()[..bytesSize];
        int size = w * h;
        IMemoryOwner<Color> destBuffer = MemoryPool<Color>.Shared.Rent(size);
        Memory<Color> mem = destBuffer.Memory[..size]; // Trim it so the loop below works properly
        if (hasAlpha) {
             LoadInner<HasAlpha>(stream, memInput.Span, mem.Span);
        } else { 
             LoadInner<NoAlpha>(stream, memInput.Span, mem.Span);
        }
        return new Texture2DUploadable(destBuffer, w, h);
    }
    
    // Abuse generics in order to get dead code elimination for optimal code on both cases.
    private static void LoadInner<T>(Stream stream, Span<byte> from, Span<Color> toI) where T : AlphaMode {
        int toIdxI = 0;
        int fromIdx = from.Length;
        while (toIdxI < toI.Length) {
            // Move the remaining data back to the start and read new data
            from[fromIdx..].CopyTo(from);
            int copyLen = from.Length - fromIdx;
            int readBytes =  stream.ReadAtLeast(from[copyLen..], from.Length - copyLen, false);
            if (readBytes != from.Length - copyLen) { // The stream is ending, flag that to the inner method
                if (readBytes == 0) break;
                from = from[..(readBytes + copyLen + refillBufferMargin)]; // Add refillBufferMargin because the decoding routine will stop once few bytes are remaining
            }
            fromIdx = 0;
            
            while (fromIdx < from.Length - refillBufferMargin && toIdxI < toI.Length) {
                // Pixel values are run length encoded, this counts the number of pixels in this line
                byte lineSize = from[fromIdx];
                Color splatValue = new();
                if (typeof(T) == typeof(HasAlpha)) {
                    // If there is a nonzero alpha, all 4 bytes are stored, if alpha is zero, a single byte is
                    byte a = from[fromIdx + 1];
                    if (a > 0) {
                        splatValue.A = a;
                        splatValue.B = from[fromIdx + 2];
                        splatValue.G = from[fromIdx + 3];
                        splatValue.R = from[fromIdx + 4];
                    }
                    fromIdx += a > 0 ? 1 + 4 : 1 + 1;
                } else {
                    splatValue.A = 255;
                    splatValue.B = from[fromIdx + 1];
                    splatValue.G = from[fromIdx + 2];
                    splatValue.R = from[fromIdx + 3];
                    fromIdx += 1+3;
                }
                toI[toIdxI] = splatValue;

                if (lineSize > 1) { // Span.Fill on a single element is much slower
                    toI[(toIdxI + 1)..(toIdxI + lineSize)].Fill(splatValue);
                }

                // Advance
                toIdxI += lineSize;
            }
        }
    }
    
    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if (disposing) {
            stream.Dispose();
        }
    }

    private interface AlphaMode;

    private struct HasAlpha : AlphaMode {
        // The structs need to be of different sizes in order to force the JIT to compile two different versions
#pragma warning disable CS0169 // Field is never used
        private int _;
#pragma warning restore CS0169 // Field is never used
    }

    private struct NoAlpha : AlphaMode;
    
    public class DataPreLoader : IPreLoader {
        private readonly IPreLoader.StreamProvider streamProvider;
        private int preW;
        private int preH;
        private bool preloaded;
        
        public DataPreLoader(IPreLoader.StreamProvider streamProvider) {
            this.streamProvider = streamProvider;
        }

        public Point? GetPreloadedSize() {
            DoPreload();
            return new Point(preW, preH);
        }

        private void DoPreload() {
            if (preloaded) return;
            preloaded = true;
            // Open the stream and dispose, seeking after preloading is much slower
            using Stream stream = streamProvider(false);
            Span<byte> read = stackalloc byte[8];
            _ = stream.Read(read);
    
            // Read the width and height
            preW = BitConverter.ToInt32(read[0..]);
            preH = BitConverter.ToInt32(read[4..]);
        }

        public TextureLoader CreateLoader() {
            return new DataTextureLoader(streamProvider(true), preW, preH);
        }
    }
}

// Loads .xnb files
public sealed class XnbTextureLoader : TextureLoader {
    private readonly string path;

    private XnbTextureLoader(string path) : base(-1 /* cannot preload Xnbs */) {
        this.path = path;
    }

    // Xnb can't really be preprocessed, so just do everything in the upload step
    protected override Texture2DUploadable ProcessDataAndPrepareUpload() {
        return new Texture2DUploadable();
    }
    
    public override Texture2D UploadTexture() {
        return Engine.Instance.Content.Load<Texture2D>(path.Replace(".xnb", ""));
    }

    public class XnbPreLoader(string path) : IPreLoader {
        public Point? GetPreloadedSize() { // Not preloadable
            return null;
        }
        
        public TextureLoader CreateLoader() {
            return new XnbTextureLoader(path);
        }
    }
}

// Generates textures from a size and color
public sealed class SizeDefinedTextureLoader : TextureLoader {
    private readonly Color color;
    private readonly int width;
    private readonly int height;
    
    private SizeDefinedTextureLoader(int width, int height, Color color) : base(width*height) {
        this.color = color;
        this.width = width;
        this.height = height;
    }

    protected override Texture2DUploadable ProcessDataAndPrepareUpload() {
        IMemoryOwner<Color> bufferOwner = MemoryPool<Color>.Shared.Rent(rentSize);
        Memory<Color> mem = bufferOwner.Memory[..rentSize];
        mem.Span.Fill(color);
        return new Texture2DUploadable(bufferOwner, width, height);
    }
    
    public class SizeDefinedPreLoader(int width, int height, Color color) : IPreLoader {
        public Point? GetPreloadedSize() {
            return new Point(width, height);
        }
        
        public TextureLoader CreateLoader() {
            return new SizeDefinedTextureLoader(width, height, color);
        }
    }
}

internal sealed class ChannelJobManager {
    private readonly Channel<Job> pipelineAsync;
    private readonly Channel<Job> pipelineSync;
    private readonly Task pipelineAsyncCompletion;
    private volatile bool asyncEnabled;
    
    public ChannelJobManager(int queueSize, int parallelism) {
        (pipelineAsync, pipelineAsyncCompletion) = BuildPipeline(queueSize, parallelism);
        (pipelineSync, _) = BuildPipeline(queueSize, 1);
        
        ThreadPool.GetMinThreads(out int workerThreads, out int ioThreads);
        if (workerThreads < 32) // Try to buff the threadpool thread count to improve cold startups
            ThreadPool.SetMinThreads(32, ioThreads);
    }
    
    public void EnableAsyncPipeline() => asyncEnabled = true;
    
    public Task<Texture2D> PostJob(TextureLoader.IPreLoader preLoader, bool async, CancellationToken ct) {
        Job job = new(preLoader, ct);
        EnqueueJob(job, async);
        return job.Task;
    }

    private void EnqueueJob(Job job, bool async) {
        if (asyncEnabled && async && pipelineAsync.Writer.TryWrite(job)) // Writes may fail because the channel was completed
            return;
        if (pipelineSync.Writer.TryWrite(job))
            return;
        
        throw new UnreachableException("Channel should ingest all messages that get sent!");
    }
    
    // There's a job stealing mechanism to try to block the main thread the least possible, to improve throughput.
    // What this method does is try stealing a given job (identified by its task) and throw it to the synchronous pipeline
    // if it's early enough.
    public async ValueTask<bool> TryMoveToPriorityPipeline(Task task) {
        if (task.AsyncState is not Job job || job.Taken) return false;
        await pipelineSync.Writer.WriteAsync(job).ConfigureAwait(false);
        return true;
    }

    // Finalizes the async pipeline, and returns a task that waits until all data is processed.
    public Task CompleteAsyncPipeline() {
        asyncEnabled = false;
        pipelineAsync.Writer.Complete();
        return pipelineAsyncCompletion;
    }
    
    // Creates the texture loading pipeline according to some parameters, the pipeline follows the following format:
    // - A channel that acts as the entry point of the pipeline.
    // - N parallel workers that do cpu bound work (where N = parallelism).
    // - A channel that all parallel workers write to, with the data to upload the textures to the gpu.
    // - A single worker that tries to batch uploads to the gpu from the previous workers.
    //   This last worker also does any necessary cleanup.
    private static (Channel<Job>, Task) BuildPipeline(int queueSize, int parallelism) {
        // Util config
        UnboundedChannelOptions unboundedOptionsSyncCont = new() { AllowSynchronousContinuations = true, };
        UnboundedChannelOptions unboundedOptionsDefault = new() { AllowSynchronousContinuations = false, };
        BoundedChannelOptions boundedOptionsSyncCont = new(queueSize * 2) { AllowSynchronousContinuations = true, };
        // Head channel, when parallelism > 1 we bound to only let the close-to-running tasks, in order to make the job stealing
        // mechanism be effective
        Channel<Job> pipelineHead = Channel.CreateUnbounded<Job>(parallelism > 1 ? unboundedOptionsDefault : unboundedOptionsSyncCont);
        Channel<(Job, TextureLoader?)> mainThreadQueue = Channel.CreateBounded<(Job, TextureLoader?)>(boundedOptionsSyncCont);
        // Array of workers
        Task[] parallelTasks = new Task[parallelism];
        for (int i = 0; i < parallelism; i++) {
            parallelTasks[i] = Task.Run(() => WorkerThreadPool(pipelineHead.Reader, mainThreadQueue.Writer));
        }
        // Connect the completions
        Task.WhenAll(parallelTasks).ContinueWith(_ => mainThreadQueue.Writer.Complete());
        Task tailTask = Task.Run(() => WorkerMainThread(mainThreadQueue.Reader, queueSize * 4));
        return (pipelineHead, tailTask);
    }
    
    // Does the cpu bound work for each job
    private static async Task WorkerThreadPool(ChannelReader<Job> jobReader, ChannelWriter<(Job, TextureLoader?)> mainThreadWorker) {
        try {
            await foreach (Job job in jobReader.ReadAllAsync().ConfigureAwait(false)) {
                if (!job.TryTakeOwnership()) {
                    continue;
                }
                (Job, TextureLoader?) jobData = await CpuLoad(job);
                await mainThreadWorker.WriteAsync(jobData).ConfigureAwait(false);
            }
        } catch (Exception e) {
            Logger.Error(nameof(TextureContentHelper), "Exception in WorkerThreadPool");
            e.LogDetailed();
            throw;
        }
    }

    // Schedules the gpu uploads to the main thread
    private static async Task WorkerMainThread(ChannelReader<(Job, TextureLoader?)> jobReader, int maxWorkSize) {
        try {
            Action cachedTaskAction = () => MainThreadWorkLoad(jobReader, maxWorkSize); // Manually cache because apparently the compiler isn't smart enough
            while (await jobReader.WaitToReadAsync().ConfigureAwait(false)) {
                await MainThreadHelper.Schedule(cachedTaskAction);
            }
        } catch (Exception e) {
            Logger.Error(nameof(TextureContentHelper), "Exception in WorkerMainPool");
            e.LogDetailed();
            throw;
        }
    }
    
    // Runs on the main thread to upload the texture data, and do cleanup
    // `maxWorkSize` exists because we do not want to create tasks that will take to long for the main thread,
    // since blocking it for too long will slow down the entire loading process.
    private static void MainThreadWorkLoad(ChannelReader<(Job, TextureLoader?)> jobReader, int maxWorkSize) {
        for (int workCount = 0; workCount < maxWorkSize && jobReader.TryRead(out (Job job, TextureLoader? loader) jobData); workCount++) {
            jobData = GpuUpload(jobData.job, jobData.loader);
            Cleanup(jobData.job, jobData.loader); // Cleanup in main thread because it's faster than going to the threadpool again to dispose 
        }
    }
    
    private static async ValueTask<(Job, TextureLoader?)> CpuLoad(Job job) {
        TextureLoader? loader = null;
        try {
            job.Ct.ThrowIfCancellationRequested();
            loader = job.PreLoader.CreateLoader();
            await loader.StartLoad(job.Ct);
            job.Ct.ThrowIfCancellationRequested();
            return (job, loader);
        } catch (Exception ex) { // Catch exceptions, and cancellations
            job.SetException(ex);
            loader?.Dispose();
            return (job, null); // Null loader means something went wrong
        }
    }
    
    private static (Job, TextureLoader?) GpuUpload(Job job, TextureLoader? loader) {
        if (loader == null) return (job, loader); // Handle exceptions and cancellability
        Texture2D ret;
        try {
            ret = loader.UploadTexture();
        } catch (Exception ex) {
            job.SetException(ex);
            loader.Dispose();
            return (job, null); // Null loader means something went wrong
        }
        job.SetResult(ret);
        return (job, loader);
    }
    
    private static void Cleanup(Job job, TextureLoader? loader) {
        try {
            loader?.Dispose();
        } catch (Exception ex) {
            Logger.Error($"{nameof(TextureContentHelper)}/{nameof(ChannelJobManager)}", "Error during cleanup in texture loading");
            Logger.LogDetailed(ex);
        }
    }
    
    // Holds the required data for a load.
    // This is a class only because we box it in the tcs.Task.AsyncState
    // and because it uses a field to synchronize job stealing
    private record Job {
        public readonly TextureLoader.IPreLoader PreLoader;
        public readonly CancellationToken Ct;
        private readonly TaskCompletionSource<Texture2D> tcs;
        private int taken;
        public Task<Texture2D> Task => tcs.Task;
        public int JobId => Task.Id;
        public bool Taken => Volatile.Read(ref taken) != 0;
        
        public Job(TextureLoader.IPreLoader PreLoader, CancellationToken Ct) {
            this.PreLoader = PreLoader;
            this.Ct = Ct;
            tcs = new TaskCompletionSource<Texture2D>(this, TaskCreationOptions.RunContinuationsAsynchronously);
        }
        
        // Attempts to steal this job, making any future calls to this method return false
        public bool TryTakeOwnership() {
            if (Taken) return false;
            return Interlocked.CompareExchange(ref taken, 1, 0) == 0;
        }

        public void SetResult(Texture2D tex) => tcs.SetResult(tex);
        public void SetException(Exception e) {
            tcs.SetException(e);
        }
    }
}

// Util class to coordinate memory usage across different threads
internal sealed class MemoryPoolMemoryLimiter<T> where T : struct {
    private static readonly MemoryOwnerWrapper NoOpDisposable = new(null, 0);
    // Should be always used in a lock
    // This could be a PriorityQueue<TaskCompletionSource<IMemoryOwner<T>>, int>, with the int being the amount of memory requested.
    // But it doesn't appear to be any faster, and there's some extra danger due to its tendency to push larger requests further back,
    // slowing down other parts.
    private readonly SortedDictionary<int, (TaskCompletionSource<IDisposable>, int)> nonPriorityQueue = new();
    private int taskId;
    private long maxMemoryUsage;
    private long currentMemoryUsage;

    public long MaxMemoryUsageUnits => maxMemoryUsage;
    
    public long MaxMemoryUsageBytes {
        get => maxMemoryUsage*Unsafe.SizeOf<T>();
        set => maxMemoryUsage = value switch {
            long.MaxValue => long.MaxValue,
            _ => value/Unsafe.SizeOf<T>()
        };
    }
    
    public MemoryPoolMemoryLimiter(long initialMemUsage) {
        maxMemoryUsage = initialMemUsage;
    }

    // Returns a task that will complete once there's enough space to allocate `minBufferSize` elements.
    public ValueTask<IDisposable> Rent(int minBufferSize, CancellationToken ct) {
        ArgumentOutOfRangeException.ThrowIfNegative(minBufferSize);
        if (MainThreadHelper.IsMainThread) { // The main thread shouldn't compete for memory
            return new ValueTask<IDisposable>(NoOpDisposable);
        }

        if (TryIncreaseCurrentMemory(minBufferSize)) { // Fast path
            return new ValueTask<IDisposable>(new MemoryOwnerWrapper(this, minBufferSize));
        } 
        
        return RentSlow(minBufferSize, ct);
    }
    
    // Try to add amount to _currentMemoryUsage if _currentMemoryUsage + amount <= _maxMemoryUsage, atomically.
    private bool TryIncreaseCurrentMemory(int amount) {
        if (amount > maxMemoryUsage) {
            throw new InvalidOperationException("Tried to rent too much memory!");
        }
        long currMemUsage = currentMemoryUsage;
        while (currMemUsage + amount <= maxMemoryUsage) {
            if (Interlocked.CompareExchange(ref currentMemoryUsage, currMemUsage + amount, currMemUsage) == currMemUsage) {
                return true;
            }
            currMemUsage = currentMemoryUsage;
        }
        return false;
    }

    // Queues the request to complete later when there's space.
    private async ValueTask<IDisposable> RentSlow(int minBufferSize, CancellationToken ct) {
        // TaskCreationOptions.RunContinuationsAsynchronously is mandatory here because calls to
        // tcs.SetResult may execute any await continuations synchronously.
        // Which means we could have recursive calls to FlushFromQueue causing the tcs to have its result set twice.
        // Furthermore, it also pushes back freeing memory so it makes everything slower.
        TaskCompletionSource<IDisposable> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        // Wishful thinking: Prioritize tasks that will hold the main thread
        lock (nonPriorityQueue)
            nonPriorityQueue.Add(taskId++, (tcs, minBufferSize));
        // Cancellability, make sure to `using` here to prevent any leaks!
        await using CancellationTokenRegistration ctr = ct.Register(t => {
            ((TaskCompletionSource<IDisposable>)t!).TrySetCanceled();
        }, tcs);
        return await tcs.Task.ConfigureAwait(false);
    }

    // Called from MemoryOwnerWrapper to try let other requests in.
    private void NotifyReturn(long returnSize) {
        long val = Interlocked.Add(ref currentMemoryUsage, -returnSize);
        Trace.Assert(val >= 0);
        FlushFromQueue();
    }

    private void FlushFromQueue() {
        lock (nonPriorityQueue) {
            List<int> toRemove = [];
            foreach ((int key, (TaskCompletionSource<IDisposable> tcs, int prio)) in nonPriorityQueue) {
                if (!TryIncreaseCurrentMemory(prio)) continue;
                tcs.SetResult(new MemoryOwnerWrapper(this, prio));
                toRemove.Add(key);
            }
            foreach (int key in toRemove) nonPriorityQueue.Remove(key);
        }
    }

    private sealed class MemoryOwnerWrapper(MemoryPoolMemoryLimiter<T>? limiter, long rentedSize) : IDisposable {
        private bool isDisposed;
        public void Dispose() {
            if (isDisposed) return;
            limiter?.NotifyReturn(rentedSize);
            isDisposed = true;
        }
    }
}

internal static class TextureContentHelper {
    private const int atlasSize = 4096 * 4096 * 4;
    internal static bool FtlToggle { get; private set; }
    internal static readonly MemoryPoolMemoryLimiter<Color> MemoryManager;
    internal static readonly ChannelJobManager Pipeline;
    
    static TextureContentHelper() {
        const int initialMemUsage = atlasSize * 4; // hardcoded for now, should be plenty to start and a good default
        MemoryManager = new MemoryPoolMemoryLimiter<Color>(initialMemUsage);
        Pipeline = new ChannelJobManager(Environment.ProcessorCount*32, Environment.ProcessorCount);
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
        if (FtlToggle) return true;
        if (CoreModule.Settings.FastTextureLoading ?? Environment.ProcessorCount >= 4) {
            long limit = (long) (CoreModule.Settings.FastTextureLoadingMaxMB * 1024f * 1024f);

            if (limit <= 0) {
                // Everest.SystemMemoryMB reports the total memory in the system, so assume a tenth will be available
                limit = (long) (Everest.SystemMemoryMB * 0.1f * 1024f * 1024f);
                // Assume that even in the worst case with 4 GB system RAM, 512 MB (= 12.5% = 1/8) are still available for texture loads.
                if (limit <= (512L * 1024L * 1024L))
                    limit = (512L * 1024L * 1024L);
            }
            // ... and even if the user forcibly lowered it below 128 MB, fall back to 128 MB as even the vanilla gameplay atlas is 64MB.
            if (limit <= (128L * 1024L * 1024L))
                limit = (128L * 1024L * 1024L);

            Logger.Info("LoadContent", $"Enabling FTL with {limit} bytes");
            FtlToggle = true;
            MemoryManager.MaxMemoryUsageBytes = limit;
            Pipeline.EnableAsyncPipeline();
            return true;
        }

        MemoryManager.MaxMemoryUsageBytes = long.MaxValue; // When running synchronously there's no reason to cap memory
        return false;
    }

    public static Task<Texture2D> CreateFTLTask(TextureLoader.IPreLoader preLoader, CancellationToken ct) {
        if (FtlToggle && preLoader.GetPreloadedSize() != null) {
            return Pipeline.PostJob(preLoader, true, ct);
        }
        return Pipeline.PostJob(preLoader, false, CancellationToken.None);
    }
}
