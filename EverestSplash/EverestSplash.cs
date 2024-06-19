using EverestSplash.SDL2;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

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
            await Task.Delay(300); // Wait a bit for the splash to load the font
            Console.WriteLine($"Running for {s} seconds...");
            StreamWriter sw = new(server);
            for (int i = 1; i < progBarSteps + 1; i++) {
                await sw.WriteLineAsync("#progress" + i + ";" + progBarSteps + ";" + "ABCDEFGHIJKLMNOPQRSTUVWXYZá");
                await sw.FlushAsync();
                await Task.Delay(s*1000/progBarSteps);
            }
            await sw.WriteLineAsync($"#finish{progBarSteps};Almost done...");
            await sw.FlushAsync();
            await Task.Delay(300);
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
    private readonly string targetRenderer;
    private readonly Assembly currentAssembly;
    private readonly CancellationTokenSource tokenSource = new();
    private bool earlyExit = false;

    private FontLoader? renogareFont;
    private LoadingProgress _loadingProgress = new(0, 0, "");

    private LoadingProgress loadingProgress {
        get => _loadingProgress;
        set {
            if (renogareFont == null) return; // Too early :/ (ignore data received when the splash was still initializing)
            _loadingProgress = value with { lastMod = value.lastMod == "" ? _loadingProgress.lastMod : value.lastMod };
            if (_loadingProgress.raw) { // .raw means no extra decorations on the mod name
                // also skip sanitization since this is not arbitrary data
                windowInfo.modLoadingProgressCache?.SetText(_loadingProgress.lastMod);
                return;
            }
            char[] sanitizedName = _loadingProgress.lastMod.ToCharArray();
            // Sanitize the sent mod name, it could contain forbidden characters
            // I KNOW I KNOW, this is absolutely slow and painful to your eyes, TWO whole string copies, and a loop, O(n*3), painful
            // so feel free to optimize it :D
            for (int i = 0; i < sanitizedName.Length; i++) {
                if (!renogareFont.IsValidChar(sanitizedName[i]))
                    sanitizedName[i] = '?'; // Fallback char
            }
            
            
            windowInfo.modLoadingProgressCache?.SetText(
                $"Loading {sanitizedName.AsSpan()} [{_loadingProgress.loadedMods}/{_loadingProgress.totalMods}]");
        }
    }

    public static EverestSplashWindow CreateNewWindow(string targetRenderer = "", string postFix = "") {
        if (instance != null)
            throw new InvalidOperationException(EverestSplash.Name + "Window created multiple times!");
        return new EverestSplashWindow(targetRenderer, postFix);
    }

    private EverestSplashWindow(string targetRendererName, string postFix) {
        instance = this;
        targetRenderer = targetRendererName;
        currentAssembly = GetType().Assembly;
        
        string serverName = EverestSplash.Name + postFix;
        Console.WriteLine("Running splash on " + serverName);
        
        ClientPipe = new NamedPipeClientStream(".", serverName);
        ClientPipe.ConnectAsync().ContinueWith(async _ => {
            try {
                StreamReader sr = new(ClientPipe);
                while (await sr.ReadLineAsync(tokenSource.Token) is { } message) {
                    if (message == "#stop") { // Stop the splash
                        break;
                    }

                    const string progressPfx = "#progress";
                    if (message.StartsWith(progressPfx)) {
                        // Mod loading progress message received: "#progress{loadedMods}{totalMods}{modName}"
                        int countEnd = message.IndexOf(";", StringComparison.Ordinal);
                        int totalEnd = message.IndexOf(";", countEnd + 1, StringComparison.Ordinal);

                        int loadedMods = int.Parse(message[progressPfx.Length..countEnd]);
                        int totalMods = int.Parse(message[(countEnd + 1)..totalEnd]);
                        loadingProgress = new LoadingProgress(loadedMods, totalMods, message[(totalEnd + 1)..]);
                    }

                    const string finishPfx = "#finish";
                    if (message.StartsWith(finishPfx)) {
                        // Mod finish progress message received: "#finish{totalMods}{message}"
                        int totalEnd = message.IndexOf(";", StringComparison.Ordinal);

                        int totalMods = int.Parse(message[finishPfx.Length..totalEnd]);
                        loadingProgress = new LoadingProgress(totalMods, totalMods, message[(totalEnd + 1)..], true);
                    }
                }

            } catch (OperationCanceledException) {
                // Swallow it
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                // We want to exit if a read error occurred, we must not be around when FNA's main loop starts
            }
            Console.WriteLine("Exiting splash...");
            SDL.SDL_Event userEvent = new() { // Fake a user event, we don't need anything fancier for now
                type = SDL.SDL_EventType.SDL_USEREVENT,
            };
            SDL.SDL_PushEvent(ref userEvent); // This is thread safe :)
        }, tokenSource.Token);

        Init(); // Init right away
    }

    public void Run() { // Calling this multiple times is asking for trouble
        // init is done in constructor
        LoadTextures();

        HandleWindow();

        Cleanup();
        
        // Exiting early means the splash was not asked to do so by Everest,
        // as such there's no need to communicate that.
        if (!earlyExit)
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
            SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED 
            | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC 
            | SDL.SDL_RendererFlags.SDL_RENDERER_TARGETTEXTURE /* Required for fast font rendering */);
        if (renderer == IntPtr.Zero)
            throw new Exception("Failed to create renderer!\n" + SDL.SDL_GetError());

        windowInfo = new WindowInfo() { window = window, renderer = renderer, };
        SDL.SDL_SetHint( SDL.SDL_HINT_RENDER_SCALE_QUALITY, "1");
        
        // Ugly part, look a way for a sec
        // This is the only place where we have a good reason to load an image into a surface, so no abstraction here
        using Stream appIconStream = File.Exists(AppIcon.path)
            ? File.OpenRead(AppIcon.path)
            : GetStreamFromEmbeddedResource(AppIcon.path);
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
        
        // Okay, good code continues here
    }

    private void LoadTextures() {
        windowInfo.everestLogoTexture =
            LoadTexture(EverestLogoTexture);
        windowInfo.wheelTexture =
            LoadTexture(WheelTexture);
        SDL.SDL_SetTextureAlphaMod(windowInfo.wheelTexture.Handle, 25);
        windowInfo.bgGradientTexture =
            LoadTexture(BgGradientTexture);
        SDL.SDL_SetTextureAlphaMod(windowInfo.bgGradientTexture.Handle, 25);
        
        // Load the font
        using (Stream fontDataStream = GetStreamFromEmbeddedResource("SplashContent/fonts/renogare.bin"))
        using (Stream fontPixelsStream = GetStreamFromEmbeddedResource("SplashContent/fonts/renogare_0.png"))
            renogareFont = new FontLoader(
                fontDataStream,
                STexture.FromStream(fontPixelsStream, windowInfo.renderer)
            );

        // Setup the font cache
        windowInfo.startingEverestFontCache = new FontCache(renogareFont);
        windowInfo.modLoadingProgressCache = new FontCache(renogareFont);
        
        windowInfo.loadedTextures.Add(renogareFont); // Not textures, but those need to be disposed as well
        windowInfo.loadedTextures.Add(windowInfo.startingEverestFontCache);
        windowInfo.loadedTextures.Add(windowInfo.modLoadingProgressCache);
    }

    private void HandleWindow() {
        SDL.SDL_ShowWindow(windowInfo.window);

        // Animation values, SDL timers are a pain to use, this is easier
        int startEverestSpriteIdx = 0;
        string[] startingCelesteText = { // DO Make sure that the longest string goes first, for caching reasons
            "Starting Celeste...", "Starting Celeste", "Starting Celeste.", "Starting Celeste.."
        };
        AnimTimer(500, () => {
            windowInfo.startingEverestFontCache.SetText(startingCelesteText[startEverestSpriteIdx]);
            startEverestSpriteIdx = (startEverestSpriteIdx + 1) % startingCelesteText.Length;
        });
        
        int realBgH = windowInfo.bgGradientTexture.Height * WindowWidth / windowInfo.bgGradientTexture.Width;
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
            if (loadingProgress.totalMods == 0) { // skip updating since it must have not initialized yet
                return;
            }
            progressWidth = (float)loadingProgress.loadedMods/loadingProgress.totalMods * WindowWidth*0.25f + prevProgress*0.75f;
            prevProgress = progressWidth;
        });

        windowInfo.modLoadingProgressCache.SetText("Loading..."); // Default to "Loading..."

        while (true) { // while true :trolloshiro: (on a serious note, for our use case its fineee :))
            
            while (SDL.SDL_PollEvent(out SDL.SDL_Event e) != 0) {
                // An SDL_USEREVENT is sent when the splash receives the quit command
                if (e.type is SDL.SDL_EventType.SDL_QUIT or SDL.SDL_EventType.SDL_USEREVENT) {
                    // SDL_QUIT is currently the only way to exit early (other that crashing)
                    if (e.type is SDL.SDL_EventType.SDL_QUIT)
                        earlyExit = true;
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
            SDL.SDL_RenderCopy(windowInfo.renderer, windowInfo.bgGradientTexture.Handle, IntPtr.Zero, ref bgRect);
            // Draw another one above because it tiles nicely
            bgRect.y = bgBloomPos - bgRect.h;
            SDL.SDL_RenderCopy(windowInfo.renderer, windowInfo.bgGradientTexture.Handle, IntPtr.Zero, ref bgRect);
            // Finally, draw another one below the first one (mostly for 16:9 mode)
            bgRect.y = bgBloomPos + bgRect.h;
            SDL.SDL_RenderCopy(windowInfo.renderer, windowInfo.bgGradientTexture.Handle, IntPtr.Zero, ref bgRect);


            // Background wheel
            float scale = (float) WindowWidth / windowInfo.wheelTexture.Width;
            SDL.SDL_Rect wheelRect = new() {
                x = (int)(-windowInfo.wheelTexture.Width*scale/2),
                y = (int)(-windowInfo.wheelTexture.Height*scale/2),
                w = (int)(windowInfo.wheelTexture.Width*scale),
                h = (int)(windowInfo.wheelTexture.Height*scale),
            };
            SDL.SDL_RenderCopyEx(windowInfo.renderer, windowInfo.wheelTexture.Handle, IntPtr.Zero,
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
                h = (int) ((float) realWindowWidth/windowInfo.everestLogoTexture.Width*windowInfo.everestLogoTexture.Height), // no need to subtract margin here since it ignores the height
            };
            SDL.SDL_RenderCopy(windowInfo.renderer, windowInfo.everestLogoTexture.Handle, IntPtr.Zero, ref everestLogoRect);


            // Render the starting everest text
            SDL.SDL_Point startingEverestPoint = new() {
                x = LRmargin,
                y = Tmargin + (everestLogoRect.y + everestLogoRect.h),
            };
            windowInfo.startingEverestFontCache.Render(windowInfo.renderer, startingEverestPoint, 0.60F);

            SDL.SDL_Point modLoadingProgressPoint = new() {
                x = LRmargin+2, // Apparently this text looks misaligned compared to the above text, likely because of the change in font size, anyhow this lessens the effect
                y = (int)(Tmargin + everestLogoRect.y + everestLogoRect.h +
                          windowInfo.startingEverestFontCache.GetCachedTextureSize().y * 0.60F),
            };
            windowInfo.modLoadingProgressCache.Render(windowInfo.renderer, modLoadingProgressPoint, 0.30F);

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
        foreach (IDisposable texture in windowInfo.loadedTextures) {
            texture.Dispose();
        }
        
        renogareFont?.Dispose();

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
        tokenSource.Cancel(); // Make sure the read thread is gone
    }

    /// <summary>
    /// Kills the window, stopping everything and releasing all resources.
    /// </summary>
    public void Kill() {
        tokenSource.Cancel();
        ClientPipe.Dispose();
        Cleanup();
    }

    /// <summary>
    /// Notifies the server that we're done.
    /// There would be no issue if we just closed the splash after the game window has been created (since its on diferent processes)
    /// But if, for some reason, the splash does not receive the stop command everest will assume it is about to close,
    /// leaving the splash alive (and confusing users), this way it is possible to know when the splash is gone, and when
    /// to kill it if it's not responding.
    /// </summary>
    private void FeedBack() {
        StreamWriter sw = new(ClientPipe);
        sw.WriteLine("done");
        sw.Flush();
        sw.Dispose();
        ClientPipe.Dispose();
        Console.WriteLine("Splash done!");
    }

    // Loads a texture from the provided sprite, defaults to local paths
    private STexture LoadTexture(TextureInfo sprite) {
        STexture tex;
        using (Stream stream = File.Exists(sprite.path) ? File.OpenRead(sprite.path) : GetStreamFromEmbeddedResource(sprite.path))
            tex = STexture.FromStream(stream, windowInfo.renderer);

        windowInfo.loadedTextures.Add(tex);
        return tex;
    }

   
    
    private Stream GetStreamFromEmbeddedResource(string embeddedResourcePath) {
        // If this project is built on Windows the embedded resource path will use backslashes
        return currentAssembly.GetManifestResourceStream(embeddedResourcePath)
             ?? currentAssembly.GetManifestResourceStream(embeddedResourcePath.Replace('/', '\\'))
             ?? throw new FileNotFoundException($"Cannot find sprite with path as embeddedResource: {embeddedResourcePath}");
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
        cb(); // Call it instantly to guarantee all cb have ran before the update loop
        Timer animTimer = new(ms);
        animTimer.Elapsed += (_, _) => { cb(); };
        animTimer.AutoReset = true;
        animTimer.Enabled = true;
        timers.Add(animTimer);
    }


    private struct WindowInfo {
        public IntPtr window = IntPtr.Zero;
        public IntPtr renderer = IntPtr.Zero;
        public STexture everestLogoTexture = null!;
        public FontCache startingEverestFontCache = null!;
        public FontCache modLoadingProgressCache = null!;
        public STexture wheelTexture = null!;
        public STexture bgGradientTexture = null!;
        public readonly List<IDisposable> loadedTextures = new();

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

    public record LoadingProgress(int loadedMods, int totalMods, string lastMod, bool raw = false);


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

