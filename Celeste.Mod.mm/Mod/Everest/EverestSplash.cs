#nullable enable
using System;
using System.IO;
using System.IO.Pipes;
using SDL2;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace EverestSplash;

/// <summary>
/// EverestSplash is a simple program whose task is to display a `loading in progress` window coded in pure sdl
/// It is designed to work together with Everest and communicate via named pipes, where it'll listen for any data,
/// and once a line can be read, the splash will disappear.
/// This program could also be loaded as a library and ran by calling `LaunchWindow`.
/// It uses a separate thread to run sdl, even if not necessary, to not accidentally create any gl, vk or directx context
/// and conflict with fna
/// For testing see message at end of file
/// </summary>
public static class EverestSplash {
    public const string Name = "EverestSplash";

    /// <summary>
    /// This method requires always the targetRenderer, for ease of use with reflection
    /// </summary>
    /// <param name="targetRenderer">the sdl2 renderer to use, "" is any renderer</param>
    /// <returns>The window created</returns>
    public static EverestSplashWindow CreateWindow(string targetRenderer) {
        return EverestSplashWindow.CreateNewWindow(targetRenderer);
    }

    public static void RunWindow(EverestSplashWindow window) {
        window.Run();
    }

    /// <summary>
    /// Launches the window, to be closes via named pipes
    /// </summary>
    public static void LaunchWindow() {
        EverestSplashWindow window = EverestSplashWindow.CreateNewWindow();
        window.Run();
    }

    public static void LaunchWindowDefault() {
        LaunchWindowSeconds(5);
    }

    /// <summary>
    /// Launches a new window, which will last s seconds
    /// </summary>
    /// <param name="s">Window lifespan</param>
    public static void LaunchWindowSeconds(int s) {
        Task.Run(async () => {
            NamedPipeServerStream server = new(Name);
            await server.WaitForConnectionAsync();
            Console.WriteLine($"Running for {s} seconds...");
            await Task.Delay(s*1000);
            StreamWriter sw = new(server);
            await sw.WriteLineAsync("stop");
            await sw.FlushAsync();
            Console.WriteLine("Close request sent");
            StreamReader sr = new(server);
            await sr.ReadLineAsync();
            Console.WriteLine("Close confirmation received");
        });
        EverestSplashWindow window = EverestSplashWindow.CreateNewWindow();
        window.Run();
    }
}

/// <summary>
/// The class responsible of holding and doing all the heavy work on the splash,
/// is instantiated via `CreateNewWindow`
/// </summary>
[SuppressMessage("Performance", "CA1806:Do not ignore method results")]
public class EverestSplashWindow {
    private static readonly string WindowTitle = "Starting Everest...";
    private static int WindowHeight = 340; // Currently hardcoded, TODO: fractional scaling
    private static int WindowWidth = 800;
    private static readonly TextureInfo EverestLogoTexture = new() {
        path = "Content/Graphics/Atlases/Gui/splash/everest_centered.png",
    };
    private static readonly TextureInfo StartingEverestTexture = new() {
        path = "Content/Graphics/Atlases/Gui/splash/starting_everest_text.png",
    };
    private static readonly TextureInfo WheelTexture = new() {
        path = "Content/Graphics/Atlases/Gui/splash/splash_wheel_blur.png",
    };
    private static readonly TextureInfo BgGradientTexture = new() {
        path = "Content/Graphics/Atlases/Gui/splash/bg_gradient_2x.png",
    };
    private static readonly TextureInfo AppIcon = new() {
        path = "../lib-stripped/Celeste-icon.png",
    };
    private static readonly Color bgDark = new() {  // Everest's dark purple color
        R = 59, G = 45, B = 74, A = 255,
    };
    private static readonly Color bgLight = new() { // Lighter color
        R = 81, G = 62, B = 101, A = 255,
    };
    private static EverestSplashWindow? instance;

    
    private readonly NamedPipeClientStream ClientPipe = new(".", EverestSplash.Name);
    private WindowInfo windowInfo;
    private readonly FNAFixes fnaFixes = new();
    private readonly string targetRenderer;

    public static EverestSplashWindow CreateNewWindow(string targetRenderer = "") {
        if (instance != null)
            throw new InvalidOperationException(EverestSplash.Name + "Window created multiple times!");
        return new EverestSplashWindow(targetRenderer);
    }

    private EverestSplashWindow(string targetRenderer) {
        instance = this;
        this.targetRenderer = targetRenderer;
        ClientPipe.ConnectAsync().ContinueWith(_ => {
            try {
                StreamReader sr = new(ClientPipe);
                sr.ReadLine(); // Once we read a line, send the stop event  (for now)
            } catch (Exception e) {
                Console.WriteLine(e);
                // We want to exit if a read error occured, we must not be around when FNA's main loop starts
            }
            SDL.SDL_Event userEvent = new() { // Fake an user event, we don't need anything fancier for now
                type = SDL.SDL_EventType.SDL_USEREVENT,
            };
            SDL.SDL_PushEvent(ref userEvent); // This is thread safe :)
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
            // TODO: Proper error handling on the thread
            throw new Exception("Failed to create SDL window!");
        }

        SDL_image.IMG_Init(SDL_image.IMG_InitFlags.IMG_INIT_PNG);

        IntPtr window = SDL.SDL_CreateWindow(WindowTitle, SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED,
            WindowWidth, WindowHeight, SDL.SDL_WindowFlags.SDL_WINDOW_BORDERLESS | SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN);

        IntPtr renderer = SDL.SDL_CreateRenderer(window, GetSDLRendererIdx(),
            SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);

        windowInfo = new WindowInfo() { window = window, renderer = renderer, };
        SDL.SDL_SetHint( SDL.SDL_HINT_RENDER_SCALE_QUALITY, "1");

        IntPtr appIconRWops = LoadRWopsFromEmbeddedResource(AppIcon.path);
        IntPtr appIconSurface = SDL_image.IMG_Load_RW(appIconRWops, (int) SDL.SDL_bool.SDL_TRUE); // Make sure to always free the RWops
        SDL.SDL_SetWindowIcon(window, appIconSurface);
        
        // Fna fixes
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
            // No value reset, its an angle anyways
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
            
            // Bg bloom drawing
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
                        
            // Present
            SDL.SDL_RenderPresent(windowInfo.renderer); // Note: this has vsync, so no sleep after this
        }
    }

    private void Cleanup() {
        fnaFixes.Dispose(); // Do this asap, theres no reason to (theoretically), but it wont hurt
        
        SDL.SDL_DestroyTexture(windowInfo.everestLogoTexture);
        SDL.SDL_DestroyTexture(windowInfo.startingEverestTexture);
        SDL.SDL_DestroyTexture(windowInfo.wheelTexture);
        SDL.SDL_DestroyTexture(windowInfo.bgGradientTexture);
        
        SDL.SDL_DestroyRenderer(windowInfo.renderer);

        SDL.SDL_DestroyWindow(windowInfo.window);
        
        // Do not call this under any circumstance when running together with everest
        // It will mess with fna and cause a hangup/segfault
        // I mean it makes sense, this un-initializes everything, something fna doesnt expect :P
        // SDL.SDL_Quit();

        foreach (Timer timer in timers) {
            timer.Stop();
            timer.Dispose();
        }
        timers.Clear();
    }

    /// <summary>
    /// Notifies the server that we're done.
    /// When running this script as a thread with another sdl app loading up, which is the case of Everest with Celeste
    /// The event loop from this program is going to mess with the one from the main app so we *must* have exited before
    /// that one starts because, in the case where that other loop eats up our stop event, disaster will strike.
    /// </summary>
    private void FeedBack() {
        StreamWriter sw = new(ClientPipe);
        sw.WriteLine("done");
        sw.Flush();
    }

    private IntPtr LoadTexture(TextureInfo sprite) {
        return File.Exists(sprite.path) ? 
            LoadTextureFromPath(sprite.path) : 
            LoadTextureFromEmbeddedResource(sprite.path);
    }

    private IntPtr LoadTextureFromPath(string path) {
        IntPtr texture = SDL_image.IMG_LoadTexture(windowInfo.renderer, path);
        if (texture.Equals(IntPtr.Zero)) {
            throw new Exception(SDL_image.IMG_GetError());
        }
        return texture;
    }

    private IntPtr LoadTextureFromEmbeddedResource(string embeddedResourcePath) {
        IntPtr rwData = LoadRWopsFromEmbeddedResource(embeddedResourcePath);
        IntPtr texture = SDL_image.IMG_LoadTexture_RW(windowInfo.renderer, rwData, (int)SDL.SDL_bool.SDL_TRUE);
        if (texture.Equals(IntPtr.Zero)) {
            throw new Exception(SDL_image.IMG_GetError());
        }
        return texture;
    }

    private IntPtr LoadRWopsFromEmbeddedResource(string embeddedResourcePath) {
        Stream? stream = GetType().Assembly.GetManifestResourceStream(embeddedResourcePath);
        if (stream == null) {
            throw new FileNotFoundException($"Cannot find sprite with path as embeddedResource: {embeddedResourcePath}");
        }

        unsafe {
            IntPtr data_ptr = Marshal.AllocHGlobal((int) (stream.Length * sizeof(byte)));
            Span<byte> data = new((byte*) data_ptr, (int) stream.Length);
            int read = stream.Read(data);
            if (read == 0) { // Basic error checking, we don't really know how many should we read anyways
                throw new InvalidDataException(
                    $"Could not read embedded resource stream for resource: {embeddedResourcePath}");
            }

            IntPtr rwData;
            fixed (byte* data_bytes = data) {
                rwData = SDL.SDL_RWFromConstMem(new IntPtr(data_bytes), read);
            }

            return rwData;
        }
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
        public IntPtr window;
        public IntPtr renderer;
        public IntPtr everestLogoTexture;
        public IntPtr startingEverestTexture;
        public IntPtr wheelTexture;
        public IntPtr bgGradientTexture;
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
    /// Simple class to manage fna fixes and modifications and easily undo them
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
    
#nullable disable
    public static class SDL_image {
        /* Used by DllImport to load the native library. */
        private const string nativeLibName = "SDL2_image";
        
        
        [Flags]
        public enum IMG_InitFlags
        {
        	IMG_INIT_JPG =	0x00000001,
        	IMG_INIT_PNG =	0x00000002,
        	IMG_INIT_TIF =	0x00000004,
        	IMG_INIT_WEBP =	0x00000008
        }
        
        [DllImport(nativeLibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int IMG_Init(IMG_InitFlags flags);
        
        /* src refers to an SDL_RWops*, IntPtr to an SDL_Surface* */
        /* THIS IS A PUBLIC RWops FUNCTION! */
        [DllImport(nativeLibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr IMG_Load_RW(
        	IntPtr src,
        	int freesrc
        );
        
        /* IntPtr refers to an SDL_Texture*, renderer to an SDL_Renderer* */
        [DllImport(nativeLibName, EntryPoint = "IMG_LoadTexture", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe IntPtr INTERNAL_IMG_LoadTexture(
        	IntPtr renderer,
        	byte* file
        );
        public static unsafe IntPtr IMG_LoadTexture(
        	IntPtr renderer,
        	string file
        ) {
        	byte* utf8File = Utf8EncodeHeap(file);
        	IntPtr handle = INTERNAL_IMG_LoadTexture(
        		renderer,
        		utf8File
        	);
        	Marshal.FreeHGlobal((IntPtr) utf8File);
        	return handle;
        }

        /* renderer refers to an SDL_Renderer*.
         * src refers to an SDL_RWops*.
         * IntPtr to an SDL_Texture*.
         */
        /* THIS IS A PUBLIC RWops FUNCTION! */
        [DllImport(nativeLibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr IMG_LoadTexture_RW(
        	IntPtr renderer,
        	IntPtr src,
			int freesrc
        );
        
        public static string IMG_GetError()
        {
        	return SDL.SDL_GetError();
        }
        
        /* Used for heap allocated string marshaling.
        * Returned byte* must be free'd with FreeHGlobal.
        */
        private static unsafe byte* Utf8EncodeHeap(string str)
        {
        	if (str == null)
        	{
        		return (byte*) 0;
        	}

        	int bufferSize = Utf8Size(str);
        	byte* buffer = (byte*) Marshal.AllocHGlobal(bufferSize);
        	fixed (char* strPtr = str)
        	{
        		Encoding.UTF8.GetBytes(strPtr, str.Length + 1, buffer, bufferSize);
        	}
        	return buffer;
        }
        
        /* Used for stack allocated string marshaling. */
        private static int Utf8Size(string str)
        {
        	if (str == null)
        	{
        		return 0;
        	}
        	return (str.Length * 4) + 1;
        }
    }
}
/* In order to modify and test this module it may be beneficial to detach it 
 * from Everest and work on it in a separate environment.
 * Consequently, to run this file you could just, if you ide supports it, 
 * run the `LaunchWindow` method. Otherwise "hacking" it and adding a `Main`
 * method and changing the output type to `Exe` is also valid for developing,
 * just make sure to revert it. Finally, theres tools that are capable of
 * loading a dll and running a method from it via cli arguments. 
 */
