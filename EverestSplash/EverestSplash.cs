using EverestSplash.SDL2;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace EverestSplash;

/// <summary>
/// EverestSplash is a simple program whose task is to display a `loading in progress` window coded in pure SDL.
/// It is designed to work together with Everest and communicate via named pipes, where it'll listen for any data,
/// and once a line can be read, the splash will disappear.
/// This program could also be loaded as a library and ran by calling `LaunchWindow`.
/// It is intended to run in another OS level process to not cause issues with any other engine (mainly FNA)
/// For testing, you can run this as a standalone, providing `--testmode (seconds)` as an argument will make it run for
/// that amount of seconds
/// </summary>
public static class EverestSplash {
    public const string Name = "EverestSplash";

    /// <summary>
    /// Main function.
    /// </summary>
    /// <param name="args">
    /// `--testmode` is inteded for testing this as a separate project, runs it for 5s,
    /// `--graphics` is the sdl_renderer to use,
    /// `--server-postfix` is a string to append to the named pipe client address to not cause issues with multiple instances
    /// </param>
    public static void Main(string[] args) {
        string targetRenderer = "";
        string postFix = "";
        int runSeconds = 0;
        for (int i = 0; i < args.Length; i++) {
            if (args[i] == "--graphics" && args.Length > i + 1) {
                targetRenderer = args[i + 1];
            } else if (args[i] == "--server-postfix" && args.Length > i + 1) {
                postFix = args[i + 1];
            } else if (args[i] == "--testmode") {
                if (args.Length > i + 1) {
                    runSeconds = int.Parse(args[i + 1]);
                } else {
                    runSeconds = 5;
                }
            }
        }

        EverestSplashWindow window = CreateWindow(targetRenderer, postFix);

        if (runSeconds != 0) {
            RunWindowSeconds(window, runSeconds);
        } else {
            window.Run();
        }
    }

    /// <summary>
    /// This method always requires the targetRenderer, for ease of use with reflection.
    /// </summary>
    /// <param name="targetRenderer">The SDL2 renderer to use or "" for any renderer.</param>
    /// <param name="postFix">A post fix to the server name, to not conflict with other instances</param>
    /// <returns>The window created.</returns>
    public static EverestSplashWindow CreateWindow(string targetRenderer = "", string postFix = "") {
        return EverestSplashWindow.CreateNewWindow(targetRenderer, postFix);
    }

    /// <summary>
    /// Launches the window, to be closed via named pipes.
    /// </summary>
    public static void LaunchWindow() {
        EverestSplashWindow window = EverestSplashWindow.CreateNewWindow();
        window.Run();
    }

    /// <summary>
    /// Runs the window, which will last s seconds.
    /// </summary>
    /// <param name="window">The window to operate on</param>
    /// <param name="s">The window lifespan.</param>
    public static void RunWindowSeconds(EverestSplashWindow window, int s, int progBarSteps = 3) {
        Task.Run(async () => {
            NamedPipeServerStream server = new(Name);
            await server.WaitForConnectionAsync();
            Console.WriteLine($"Running for {s} seconds...");
            StreamWriter sw = new(server);
            for (int i = 1; i < progBarSteps + 1; i++) {
                await sw.WriteLineAsync("#progress" + (float)i / progBarSteps);
                await sw.FlushAsync();
                await Task.Delay(s*1000/progBarSteps);
            }
            await sw.WriteLineAsync("#stop");
            await sw.FlushAsync();
            Console.WriteLine("Close request sent");
            StreamReader sr = new(server);
            await sr.ReadLineAsync();
            Console.WriteLine("Close confirmation received");
        });
        window.Run();
    }
}

/// <summary>
/// The class responsible for holding and doing all the heavy work on the splash.
/// Instantiated via `CreateNewWindow`.
/// </summary>
public class EverestSplashWindow {
    private static readonly string WindowTitle = "Starting Everest...";
    private static int WindowHeight = 340; // Currently hardcoded, TODO: fractional scaling
    private static int WindowWidth = 800;
    private static readonly TextureInfo EverestLogoTexture = new() {
        path = "SplashContent/everest_centered.png",
    };
    private static readonly TextureInfo StartingEverestTexture = new() {
        path = "SplashContent/starting_everest_text.png",
    };
    private static readonly TextureInfo WheelTexture = new() {
        path = "SplashContent/splash_wheel_blur.png",
    };
    private static readonly TextureInfo BgGradientTexture = new() {
        path = "SplashContent/bg_gradient_2x.png",
    };
    private static readonly TextureInfo AppIcon = new() {
        path = "../lib-stripped/Celeste-icon.png",
    };
    private static readonly Color bgDark = new() {  // Everest's dark purple color
        R = 59, G = 45, B = 74, A = 255,
    };
    private static readonly Color bgLight = new() { // Lighter color
        R = 63, G = 48, B = 79, A = 255,
    };
    private static EverestSplashWindow? instance;


    private readonly NamedPipeClientStream ClientPipe;
    private WindowInfo windowInfo;
    private readonly FNAFixes fnaFixes = new();
    private readonly string targetRenderer;
    private readonly Assembly currentAssembly;

    private float loadingProgress = 0;

    public static EverestSplashWindow CreateNewWindow(string targetRenderer = "", string postFix = "") {
        if (instance != null)
            throw new InvalidOperationException(EverestSplash.Name + "Window created multiple times!");
        return new EverestSplashWindow(targetRenderer, postFix);
    }

    private EverestSplashWindow(string targetRenderer, string postFix) {
        instance = this;
        this.targetRenderer = targetRenderer;
        currentAssembly = GetType().Assembly;
        string serverName = EverestSplash.Name + postFix;
        Console.WriteLine("Running splash on " + serverName);
        ClientPipe = new(".", serverName);
        ClientPipe.ConnectAsync().ContinueWith(_ => {
            try {
                StreamReader sr = new(ClientPipe);
                while (sr.ReadLine() is { } message) {
                    if (message == "#stop") { // Stop the splash
                        break;
                    }

                    const string progressPfx = "#progress";
                    if (message.StartsWith(progressPfx)) { // Mod loading progress message received: "progress (float){progress}"
                        loadingProgress = float.Parse(message[progressPfx.Length..]);
                    }
                }
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                // We want to exit if a read error occurred, we must not be around when FNA's main loop starts
            }
            SDL.SDL_Event userEvent = new() { // Fake a user event, we don't need anything fancier for now
                type = SDL.SDL_EventType.SDL_USEREVENT,
            };
            SDL.SDL_PushEvent(ref userEvent); // This is thread safe :)
            Console.WriteLine("Exiting splash...");
        });

        Init(); // Init right away
    }

    public void Run() { // Calling this multiple times is asking for trouble
        // init is done in constructor
        LoadTextures();

        HandleWindow();

        Cleanup();

        FeedBack();
    }

    private void Init() {
        // Before init, check if we're on gamescope
        string? xdgCurrentDesk = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        if (xdgCurrentDesk == "gamescope") {
            // If so, default to 720p on 16:9
            WindowHeight = 720;
            WindowWidth = (int) (720.0 * 16 / 9); // 1280
        }

        if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_EVENTS) != 0) { // Init as little as we can, we need to go fast
            throw new Exception("Failed to SDL init!\n" + SDL.SDL_GetError());
        }

        IntPtr window = SDL.SDL_CreateWindow(WindowTitle, SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED,
            WindowWidth, WindowHeight, SDL.SDL_WindowFlags.SDL_WINDOW_BORDERLESS | SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN);
        if (window == IntPtr.Zero)
            throw new Exception("Failed to create window!\n" + SDL.SDL_GetError());

        IntPtr renderer = SDL.SDL_CreateRenderer(window, GetSDLRendererIdx(),
            SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
        if (renderer == IntPtr.Zero)
            throw new Exception("Failed to create renderer!\n" + SDL.SDL_GetError());

        windowInfo = new WindowInfo() { window = window, renderer = renderer, };
        SDL.SDL_SetHint( SDL.SDL_HINT_RENDER_SCALE_QUALITY, "1");

        using Stream appIconStream = GetStreamFromEmbeddedResource(AppIcon.path);
        IntPtr appIconPixels = FNA3D.ReadImageStream(appIconStream, out int w, out int h, out int _);
        if (appIconPixels == IntPtr.Zero) 
            throw new Exception("Could not read stream!");
                
        IntPtr appIconSurface = SDL.SDL_CreateRGBSurfaceFrom(appIconPixels,
            w, 
            h, 
            8 * 4 /* byte per 4 channels */, 
            w * 4, 
            0x000000FF, 
            0x0000FF00,
            0x00FF0000, 
            0xFF000000);
        if (appIconSurface == IntPtr.Zero) 
            throw new Exception("Could not create surface! " + SDL.SDL_GetError());
        SDL.SDL_SetWindowIcon(window, appIconSurface);
        SDL.SDL_FreeSurface(appIconSurface); // Here the surface has already been copied so its safe to free
        FNA3D.FNA3D_Image_Free(appIconPixels);

        // FNA fixes
        // FNA disables the cursor on game creation and when creating the window
        fnaFixes.Add(
            new FNAFixes.FNAFix(
            () => SDL.SDL_ShowCursor(SDL.SDL_QUERY) == SDL.SDL_DISABLE,
            () => SDL.SDL_ShowCursor(SDL.SDL_ENABLE),
            () => SDL.SDL_ShowCursor(SDL.SDL_DISABLE)
            )
        );
    }

    private void LoadTextures() {
        windowInfo.everestLogoTexture =
            LoadTexture(EverestLogoTexture);
        windowInfo.startingEverestTexture =
            LoadTexture(StartingEverestTexture);
        windowInfo.wheelTexture =
            LoadTexture(WheelTexture);
        SDL.SDL_SetTextureAlphaMod(windowInfo.wheelTexture, 25);
        windowInfo.bgGradientTexture =
            LoadTexture(BgGradientTexture);
        SDL.SDL_SetTextureAlphaMod(windowInfo.bgGradientTexture, 25);
    }

    private void HandleWindow() {
        // Query all the textures in one go, as those cannot change
        SDL.SDL_QueryTexture(windowInfo.bgGradientTexture, out _, out _, out int bgW, out int bgH);
        SDL.SDL_QueryTexture(windowInfo.wheelTexture, out _, out _, out int wheelW, out int wheelH);
        SDL.SDL_QueryTexture(windowInfo.everestLogoTexture, out _, out _, out int logoW, out int logoH);
        SDL.SDL_QueryTexture(windowInfo.startingEverestTexture, out _, out _, out int textW, out int allTextH);

        SDL.SDL_ShowWindow(windowInfo.window);

        // Animation values, SDL timers are a pain to use, this is easier
        int startEverestSpriteIdx = 0;
        AnimTimer(500, () => {
            startEverestSpriteIdx = (startEverestSpriteIdx + 1) % 3/*startEverestSpriteCount*/;
        });
        int realBgH = bgH * WindowWidth / bgW;
        int bgBloomPos = -realBgH/2;
        AnimTimer(16, () => {
            bgBloomPos += 1;
            if (bgBloomPos > realBgH/2) {
                bgBloomPos = -realBgH/2;
            }
        });
        double wheelAngle = 0;
        AnimTimer(16, () => {
            wheelAngle += 0.1;
            // No value reset, it's an angle anyways
        });
        float progressWidth = 0;
        float prevProgress = 0;
        AnimTimer(16, () => {
            progressWidth = loadingProgress * WindowWidth*0.25f + prevProgress*0.75f;
            prevProgress = progressWidth;
        });

        while (true) { // while true :trolloshiro: (on a serious note, for our use case its fineee :))
            fnaFixes.CheckAndFix();

            while (SDL.SDL_PollEvent(out SDL.SDL_Event e) != 0) {
                // An SDL_USEREVENT is sent when the splash receives the quit command
                if (e.type is SDL.SDL_EventType.SDL_QUIT or SDL.SDL_EventType.SDL_USEREVENT) {
                    return; // quit asap
                }
            }

            // BG color generation
            Color bgColor = bgDark;
            SDL.SDL_SetRenderDrawColor(windowInfo.renderer, bgColor.R, bgColor.G, bgColor.B, bgColor.A);
            SDL.SDL_RenderClear(windowInfo.renderer);

            // BG bloom drawing
            SDL.SDL_Rect bgRect = new() {
                x = 0,
                y = bgBloomPos,
                w = WindowWidth,
                h = realBgH, // We calculated this earlier for the animation
            };
            SDL.SDL_RenderCopy(windowInfo.renderer, windowInfo.bgGradientTexture, IntPtr.Zero, ref bgRect);
            // Draw another one above because it tiles nicely
            bgRect.y = bgBloomPos - bgRect.h;
            SDL.SDL_RenderCopy(windowInfo.renderer, windowInfo.bgGradientTexture, IntPtr.Zero, ref bgRect);
            // Finally, draw another one below the first one (mostly for 16:9 mode)
            bgRect.y = bgBloomPos + bgRect.h;
            SDL.SDL_RenderCopy(windowInfo.renderer, windowInfo.bgGradientTexture, IntPtr.Zero, ref bgRect);


            // Background wheel
            float scale = (float) WindowWidth / wheelW;
            SDL.SDL_Rect wheelRect = new() {
                x = (int)(-wheelW*scale/2),
                y = (int)(-wheelH*scale/2),
                w = (int)(wheelW*scale),
                h = (int)(wheelH*scale),
            };
            SDL.SDL_RenderCopyEx(windowInfo.renderer, windowInfo.wheelTexture, IntPtr.Zero,
                ref wheelRect, wheelAngle, IntPtr.Zero, SDL.SDL_RendererFlip.SDL_FLIP_NONE);


            // Render one sprite
            const int LRmargin = 32*2; // Left right margin
            const int Tmargin = 32; // Top margin
            // Bottom margin is missing since that one is adjusted via window height
            int realWindowWidth = WindowWidth - LRmargin*2; // apply at both sides
            SDL.SDL_Rect everestLogoRect = new() {
                x = LRmargin, // Add some margin
                y = Tmargin,
                w = realWindowWidth,
                h = (int) ((float) realWindowWidth/logoW*logoH), // no need to subtract margin here since it ignores the height
            };
            SDL.SDL_RenderCopy(windowInfo.renderer, windowInfo.everestLogoTexture, IntPtr.Zero, ref everestLogoRect);



            // Render the other
            realWindowWidth /= 2; // Make it half the width
            int textH = allTextH / 3; // theres 3 texts
            SDL.SDL_Rect startingEverestRect = new() {
                x = LRmargin,
                y = Tmargin + (everestLogoRect.y+everestLogoRect.h),
                w = realWindowWidth,
                h = (int) ((float) realWindowWidth/textW*textH),
            };
            SDL.SDL_Rect sourceStartingEverestRect = new() {
                x = 0,
                y = startEverestSpriteIdx*textH,
                w = textW,
                h = textH,
            };
            SDL.SDL_RenderCopy(windowInfo.renderer, windowInfo.startingEverestTexture,
                ref sourceStartingEverestRect, ref startingEverestRect);
            
            // Render the loading progress bar
            const int barHeight = 4;
            SDL.SDL_Rect progressRect = new() {
                x = 0,
                y = WindowHeight - barHeight,
                w = (int) progressWidth,
                h = barHeight,
            };
            SDL.SDL_SetRenderDrawColor(windowInfo.renderer, 255, 255, 255, 255); // White
            SDL.SDL_RenderFillRect(windowInfo.renderer, ref progressRect);

            // Present
            SDL.SDL_RenderPresent(windowInfo.renderer); // Note: this has vsync, so no sleep after this
        }
    }

    private void Cleanup() {
        fnaFixes.Dispose(); // Do this asap, theres no reason to (theoretically), but it wont hurt

        foreach (IntPtr texture in windowInfo.loadedTextures) {
            if (texture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(texture);
        }

        if (windowInfo.renderer != IntPtr.Zero)
            SDL.SDL_DestroyRenderer(windowInfo.renderer);

        if (windowInfo.window != IntPtr.Zero)
            SDL.SDL_DestroyWindow(windowInfo.window);

        // Do not call this under any circumstance when running together with Everest
        // It will mess with FNA and cause a hangup/segfault
        // I mean it makes sense, this un-initializes everything, something FNA doesn't expect :P
        SDL.SDL_Quit();

        foreach (Timer timer in timers) {
            timer.Stop();
            timer.Dispose();
        }
        timers.Clear();
    }

    /// <summary>
    /// Kills the window, stopping everything and releasing all resources.
    /// </summary>
    public void Kill() {
        ClientPipe.Dispose();
        Cleanup();
    }

    /// <summary>
    /// Notifies the server that we're done.
    /// There would be no issue if we just closed the splash after the game window has been created (since its on diferent processes)
    /// But if, for some reason, the splash does not recieve the stop command everest will assume it is about to close,
    /// leaving the splash alive (and confusing users), this way it is possible to know when the splash is gone, and when
    /// to kill it if its not responding.
    /// </summary>
    private void FeedBack() {
        StreamWriter sw = new(ClientPipe);
        sw.WriteLine("done");
        sw.Flush();
        Console.WriteLine("Splash done!");
    }

    // Loads a texture from the provided sprite, defaults to local paths
    private IntPtr LoadTexture(TextureInfo sprite) {
        IntPtr tex = File.Exists(sprite.path) ?
            LoadTextureFromPathFNA3D(sprite.path) :
            LoadTextureFromEmbeddedResourceFNA3D(sprite.path);
        if (tex != IntPtr.Zero)
            windowInfo.loadedTextures.Add(tex);
        return tex;
    }

    // Uses FNA3D to load an SDL Texture from a Stream
    private IntPtr LoadTextureFromStreamFNA3D(Stream stream) {
        IntPtr pixels = FNA3D.ReadImageStream(stream, out int w, out int h, out int _);
        if (pixels == IntPtr.Zero) 
            throw new Exception("Could not read stream!");
        
        IntPtr surface = SDL.SDL_CreateRGBSurfaceFrom(pixels,
            w, 
            h, 
            8 * 4 /* byte per 4 channels */, 
            w * 4, 
            0x000000FF, 
            0x0000FF00,
            0x00FF0000, 
            0xFF000000);
        if (surface == IntPtr.Zero) 
            throw new Exception("Could not create surface! " + SDL.SDL_GetError());
        
        IntPtr texture = SDL.SDL_CreateTextureFromSurface(windowInfo.renderer, surface);
        if (texture == IntPtr.Zero)
            throw new Exception("Could not create texture from surface! " + SDL.SDL_GetError());
        SDL.SDL_FreeSurface(surface);
        FNA3D.FNA3D_Image_Free(pixels);
        return texture;
    }
    
    private Stream GetStreamFromEmbeddedResource(string embeddedResourcePath) {
        // If this project is built on Windows the embedded resource path will use backslashes
        return currentAssembly.GetManifestResourceStream(embeddedResourcePath)
             ?? currentAssembly.GetManifestResourceStream(embeddedResourcePath.Replace('/', '\\'))
             ?? throw new FileNotFoundException($"Cannot find sprite with path as embeddedResource: {embeddedResourcePath}");
    }
    private IntPtr LoadTextureFromEmbeddedResourceFNA3D(string embeddedResourcePath) {
        using Stream stream = GetStreamFromEmbeddedResource(embeddedResourcePath);
        return LoadTextureFromStreamFNA3D(stream);
    }

    private IntPtr LoadTextureFromPathFNA3D(string path) {
        using Stream stream = File.OpenRead(path);
        return LoadTextureFromStreamFNA3D(stream);
    }

    private int GetSDLRendererIdx() {
        if (targetRenderer == "") // empty means any driver
            return -1;
        int renderDrivers = SDL.SDL_GetNumRenderDrivers();
        for (int i = 0; i < renderDrivers; i++) {
            SDL.SDL_GetRenderDriverInfo(i, out SDL.SDL_RendererInfo info);
            if (Marshal.PtrToStringUTF8(info.name) == targetRenderer.ToLower()) {
                return i;
            }
        }
        Console.WriteLine($"Renderer target: {targetRenderer} not found or available");
        return -1; // requested renderer is not available, use anything
    }

    private List<Timer> timers = new();
    private void AnimTimer(int ms, Action cb) {
        Timer animTimer = new(ms);
        animTimer.Elapsed += (_, _) => { cb(); };
        animTimer.AutoReset = true;
        animTimer.Enabled = true;
        timers.Add(animTimer);
    }


    private struct WindowInfo {
        public IntPtr window = IntPtr.Zero;
        public IntPtr renderer = IntPtr.Zero;
        public IntPtr everestLogoTexture = IntPtr.Zero;
        public IntPtr startingEverestTexture = IntPtr.Zero;
        public IntPtr wheelTexture = IntPtr.Zero;
        public IntPtr bgGradientTexture = IntPtr.Zero;
        public readonly List<IntPtr> loadedTextures = new();

        public WindowInfo() {
        }
    }

    private struct TextureInfo {
        public string path = "";
        public TextureInfo() {}
    }

    private struct Color {
        public byte R, G, B, A;
    }

    private static Color LerpColor(Color s, Color e, float p) {
        return new Color {
            R = LerpByte(s.R, e.R, p),
            G = LerpByte(s.G, e.G, p),
            B = LerpByte(s.B, e.B, p),
            A = LerpByte(s.A, e.A, p),
        };
    }

    private static byte LerpByte(byte s, byte e, float p) {
        return (byte)(s + (e - s) * p);
    }

    /// <summary>
    /// Simple class to manage FNA fixes and modifications and easily undo them
    /// </summary>
    public class FNAFixes : IDisposable {
        private readonly List<FNAFix> fixes = new();

        public void Add(FNAFix fnaFix) {
            fixes.Add(fnaFix);
        }

        public void CheckAndFix() {
            foreach (FNAFix fix in fixes) {
                if (fix.Predicate()) {
                    fix.HasRan();
                    fix.Fix();
                }
            }
        }

        public void Dispose() {
            foreach (FNAFix fix in fixes) {
                if (fix.HasFixed)
                    fix.Undo();
            }
        }
        public record FNAFix(Func<bool> Predicate, Action Fix, Action Undo) {
            public bool HasFixed { get; private set; }
            public void HasRan() => HasFixed = true;
        }
    }

    /// <summary>
    /// Stripped down version of https://github.com/FNA-XNA/FNA/blob/master/src/Graphics/FNA3D.cs, suited for our needs.
    /// </summary>
    public static class FNA3D {
        
        #region FNA3D Bindings 
        private const string nativeLibName = "FNA3D"; 
        
        [DllImport(nativeLibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr FNA3D_Image_Load(
            FNA3D_Image_ReadFunc readFunc,
            FNA3D_Image_SkipFunc skipFunc,
            FNA3D_Image_EOFFunc eofFunc,
            IntPtr context,
            out int width,
            out int height,
            out int len,
            int forceW,
            int forceH,
            byte zoom
        );
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int FNA3D_Image_ReadFunc(
            IntPtr context,
            IntPtr data,
            int size
        );

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FNA3D_Image_SkipFunc(
            IntPtr context,
            int n
        );

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int FNA3D_Image_EOFFunc(IntPtr context);
        
        [DllImport(nativeLibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FNA3D_Image_Free(IntPtr mem);

        #endregion

        #region Image Loading
        private static int INTERNAL_Read(
            IntPtr context,
            IntPtr data,
            int size
        ) {
            Stream stream;
            lock (readStreams)
            {
                stream = readStreams[context];
            }
            byte[] buf = new byte[size]; // FIXME: Preallocate!
            int result = stream.Read(buf, 0, size);
            Marshal.Copy(buf, 0, data, result);
            return result;
        }

        private static void INTERNAL_Skip(IntPtr context, int n)
        {
            Stream stream;
            lock (readStreams)
            {
                stream = readStreams[context];
            }
            stream.Seek(n, SeekOrigin.Current);
        }

        private static int INTERNAL_EOF(IntPtr context)
        {
            Stream stream;
            lock (readStreams)
            {
                stream = readStreams[context];
            }
            return (stream.Position == stream.Length) ? 1 : 0;
        }
        
        private static FNA3D_Image_ReadFunc readFunc = INTERNAL_Read;
        private static FNA3D_Image_SkipFunc skipFunc = INTERNAL_Skip;
        private static FNA3D_Image_EOFFunc eofFunc = INTERNAL_EOF;

        private static int readGlobal = 0;
        private static Dictionary<IntPtr, Stream> readStreams = new();

        public static IntPtr ReadImageStream(
            Stream stream,
            out int width,
            out int height,
            out int len,
            int forceW = -1,
            int forceH = -1,
            bool zoom = false
        ) {
            IntPtr context;
            lock (readStreams)
            {
                context = (IntPtr) readGlobal++;
                readStreams.Add(context, stream);
            }
            IntPtr pixels = FNA3D_Image_Load(
                readFunc,
                skipFunc,
                eofFunc,
                context,
                out width,
                out height,
                out len,
                forceW,
                forceH,
                (byte) (zoom ? 1 : 0)
            );
            lock (readStreams)
            {
                readStreams.Remove(context);
            }
            return pixels;
        }

        #endregion
    }
}

