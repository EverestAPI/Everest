using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using SDL2;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace EverestSplash;

/// <summary>
/// EverestSplash is a simple program whose task is to display a `loading in progress` window coded in pure sdl
/// It is designed to work together with Everest and communicate via named pipes, where it'll listen for any data,
/// and once a line can be read, the splash will disappear.
/// This program could also be loaded as a library and ran by calling `LaunchWindow`.
/// It uses a separate thread to run sdl, even if not necessary, to not accidentally create any gl, vk or directx context
/// and conflict with fna
/// </summary>
public static class EverestSplash {
    public const string Name = "EverestSplash";

    public static void Main(string[] args) {
        for (int i = 0; i < args.Length; i++) {
            if (args[i] == "--testmode") {
                int delay = 5000;
                if (args.Length > i + 1) {
                    try {
                        delay = int.Parse(args[i + 1]);
                    } catch (Exception) {
                        // NOOP: ignore invalid numbers
                    }
                }
                Task.Run(async () => {
                    NamedPipeServerStream server = new(Name, PipeDirection.Out);
                    await server.WaitForConnectionAsync();

                    await Task.Delay(delay);

                    await using (StreamWriter sw = new(server)) {
                        await sw.WriteLineAsync("test");
                    }
                });
                break;
            }
        }
        
        LaunchWindow();
    }

    /// <summary>
    /// Launches the window, to be closes via named pipes
    /// </summary>
    public static void LaunchWindow() {
        EverestSplashWindow window = EverestSplashWindow.CreateNewWindow();
        window.StartOnThread();
    }
}

/// <summary>
/// The class responsible of holding and doing all the heavy work on the splash,
/// is instantiated via `CreateNewWindow`
/// </summary>
[SuppressMessage("Performance", "CA1806:Do not ignore method results")]
public class EverestSplashWindow {
    private readonly NamedPipeClientStream ClientPipe = new(".", EverestSplash.Name, PipeDirection.In);
    private static readonly string WindowTitle = "Starting Everest...";
    private static readonly int WindowHeight = 340; // Currently hardcoded, TODO: fractional scaling
    private static readonly int WindowWidth = 800;
    private static readonly TextureInfo EverestLogoTexture = new() {
        path = "SplashContent/everest.png"
    };
    private static readonly TextureInfo StartingEverestTexture = new() {
        path = "SplashContent/starting_everest_text.png"
    };
    private static readonly TextureInfo WheelTexture = new() {
        path = "SplashContent/splash_wheel_blur.png"
    };
    private static readonly TextureInfo BgGradientTexture = new() {
        path = "SplashContent/bg_gradient_2x.png"
    };
    private static readonly Color bgDark = new() {  // Everest's dark purple color
        R = 59, G = 45, B = 74, A = 255,
    };
    private static readonly Color bgLight = new() { // Lighter color
        R = 81, G = 62, B = 101, A = 255,
    };
    private static EverestSplashWindow? instance;

    private WindowInfo windowInfo;

    private readonly Thread thread;

    public static EverestSplashWindow CreateNewWindow() {
        if (instance != null)
            throw new InvalidOperationException(EverestSplash.Name + "Window created multiple times!");
        return new EverestSplashWindow();
    }

    private EverestSplashWindow() {
        instance = this;
        thread = new Thread(Run) {
            Name = EverestSplash.Name
        };
        ClientPipe.ConnectAsync().ContinueWith(_ => {
            Console.WriteLine("Connected with splash!");
            using StreamReader sr = new(ClientPipe);
            sr.ReadLine(); // Once we read a line, send the stop event  (for now)
            SDL.SDL_Event userEvent = new() { // Fake an user event, we don't need anything fancier for now
                type = SDL.SDL_EventType.SDL_USEREVENT,
            };
            SDL.SDL_PushEvent(ref userEvent); // This is thread safe :)
        });
    }

    public void StartOnThread() {
        if (thread.IsAlive) return; // Ignore extra starts
        thread.Start();
    }

    private void Run() {
        Init();
        
        LoadTextures();
        
        HandleWindow();
        
        Cleanup();
    }

    private void Init() {
        if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_EVENTS) != 0) { // Init as little as we can, we need to go fast
            // TODO: Proper error handling on the thread
            throw new Exception("Failed to create SDL window!");
        }

        SDL_image.IMG_Init(SDL_image.IMG_InitFlags.IMG_INIT_PNG);

        IntPtr window = SDL.SDL_CreateWindow(WindowTitle, SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED,
            WindowWidth, WindowHeight, SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN);
        // TODO: use same engine as fna for rendering?

        IntPtr renderer = SDL.SDL_CreateRenderer(window, -1,
            SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);

        windowInfo = new WindowInfo() { window = window, renderer = renderer, };
        SDL.SDL_SetHint( SDL.SDL_HINT_RENDER_SCALE_QUALITY, "1" );
        SDL.SDL_SetWindowBordered(window, SDL.SDL_bool.SDL_FALSE);
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
        SDL.SDL_ShowWindow(windowInfo.window);

        // Animation values, SDL timers are a pain to use, this is easier
        int startEverestSpriteIdx = 0;
        AnimTimer(500, () => {
            startEverestSpriteIdx = (startEverestSpriteIdx + 1) % 3/*startEverestSpriteCount*/;
        });
        float bgFloat = Random.Shared.NextSingle();
        int bgBloomPos = -WindowHeight;
        AnimTimer(16, () => {
            bgBloomPos += 1;
            if (bgBloomPos > WindowHeight) {
                bgBloomPos = -WindowHeight;
            }
        });
        double wheelAngle = 0;
        AnimTimer(16, () => {
            wheelAngle += 0.1;
            // No value reset, its an angle anyways
        });
        
        
        while (true) { // while true :trolloshiro: (on a serious note, for our use case its fineee :))
            while (SDL.SDL_PollEvent(out SDL.SDL_Event e) != 0) {
                // An SDL_USEREVENT is sent when the splash receives the quit command
                if (e.type is SDL.SDL_EventType.SDL_QUIT or SDL.SDL_EventType.SDL_USEREVENT) {
                    return; // quit asap
                }
            }

            // BG color generation
            Color bgColor = LerpColor(bgDark, bgLight, MathF.Abs(bgFloat));
            SDL.SDL_SetRenderDrawColor(windowInfo.renderer, bgColor.R, bgColor.G, bgColor.B, bgColor.A);
            SDL.SDL_RenderClear(windowInfo.renderer);
            
            // Bg bloom drawing
            SDL.SDL_Rect bgRect = new() {
                x = 0,
                y = bgBloomPos,
                w = WindowWidth,
                h = WindowHeight*2,
            };
            SDL.SDL_RenderCopy(windowInfo.renderer, windowInfo.bgGradientTexture, IntPtr.Zero, ref bgRect);
            // Draw another one above because it tiles nicely
            bgRect.y = bgBloomPos - WindowHeight * 2;
            SDL.SDL_RenderCopy(windowInfo.renderer, windowInfo.bgGradientTexture, IntPtr.Zero, ref bgRect);
            
            // Background wheel
            SDL.SDL_QueryTexture(windowInfo.wheelTexture, out _, out _, out int wheelW, out int wheelH);
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
            SDL.SDL_QueryTexture(windowInfo.everestLogoTexture, out _, out _, out int logoW, out int logoH);
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
            SDL.SDL_QueryTexture(windowInfo.startingEverestTexture, out _, out _, out int textW, out int allTextH);
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
        SDL.SDL_DestroyTexture(windowInfo.everestLogoTexture);
        SDL.SDL_DestroyTexture(windowInfo.startingEverestTexture);
        SDL.SDL_DestroyTexture(windowInfo.wheelTexture);
        SDL.SDL_DestroyTexture(windowInfo.bgGradientTexture);
        
        SDL.SDL_DestroyRenderer(windowInfo.renderer);

        SDL.SDL_DestroyWindow(windowInfo.window);
        
        SDL.SDL_Quit();

        foreach (Timer timer in timers) {
            timer.Stop();
        }
        timers.Clear();
    }

    private IntPtr LoadTexture(TextureInfo sprite) {
        IntPtr texture = SDL_image.IMG_LoadTexture(windowInfo.renderer, sprite.path);
        if (texture.Equals(IntPtr.Zero)) {
            Console.WriteLine(SDL_image.IMG_GetError());
        }
        return texture;
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
        public string path;
    }

    public struct Color {
        public byte R, G, B, A;
    }

    public static Color LerpColor(Color s, Color e, float p) {
        return new Color {
            R = LerpByte(s.R, e.R, p), 
            G = LerpByte(s.G, e.G, p), 
            B = LerpByte(s.B, e.B, p), 
            A = LerpByte(s.A, e.A, p), 
        };
    }

    public static byte LerpByte(byte s, byte e, float p) {
        return (byte)(s + (e - s) * p);
    }
}