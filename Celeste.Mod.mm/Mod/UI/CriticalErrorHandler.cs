using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Celeste.Mod.UI {
    public sealed class CriticalErrorHandler : Overlay, IDisposable {

        private static readonly FieldInfo f_Engine_scene = typeof(Engine).GetField("scene", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo f_Engine_nextScene = typeof(Engine).GetField("nextScene", BindingFlags.NonPublic | BindingFlags.Instance);

        private sealed class BeforeRenderInterceptor : Renderer {
            public Action OnBeforeRender;
            public BeforeRenderInterceptor(Action onBeforeRender) => OnBeforeRender = onBeforeRender;
            public override void BeforeRender(Scene scene) => OnBeforeRender();
        }

        public enum DisplayState {
            Initial,
            Overlay,
            CleanScene,
            BlueScreen
        }

        private enum UserChoice {
            FlushSaveData,
            RetryLevel,
            SaveAndQuit,
            ReturnToMainMenu
        }

        public static CriticalErrorHandler CurrentHandler { get; private set; }

        public static ExceptionDispatchInfo HandleCriticalError(ExceptionDispatchInfo error) {
            if (!CoreModule.Settings.UseInGameCrashHandler)
                return error;

            if (Debugger.IsAttached) {
                Trace.WriteLine($">>> CRITICAL ERROR: {error.SourceException.ToString()}");
                Debugger.Break();
            }

            Logger.Log(LogLevel.Error, "crit-error-handler", ">>>>>>>>>>>>>>> ENCOUNTERED A CRITICAL ERROR <<<<<<<<<<<<<<<");
            Logger.LogDetailed(error.SourceException, "crit-error-handler");

            // If there's no error handler yet, create one
            if (CurrentHandler == null) {
                CurrentHandler = new CriticalErrorHandler(error, BackupLogFile(error.SourceException, out string logFileError), logFileError);
            } else {
                CurrentHandler.EncounteredAdditionalErrors = true;
                AmendLogFile(CurrentHandler.LogFile, "Encountered an additional critical error", error.SourceException);
            }

            // Invoke the critical error event
            Everest.Events.CriticalError(CurrentHandler);

            // If this is a compatible scene, display as an overlay
            if (CurrentHandler.State < DisplayState.Overlay && Celeste.Scene is Level lvl) {
                CurrentHandler.State = DisplayState.Overlay;
                lvl.Add(CurrentHandler);

                // Remove any screen wipes
                if (lvl.Wipe != null) {
                    lvl.RendererList.Remove(lvl.Wipe);
                    lvl.Wipe = null;
                }

                foreach (Renderer renderer in lvl.RendererList.Renderers.ToArray())
                    if (renderer is ScreenWipe)
                        lvl.RendererList.Remove(renderer);

                return null;
            }

            // If we are currently displaying as an overlay, switch to a separate scene to avoid it crashing again
            static void DoImmediateSceneSwitch(Scene newScene) {
                Celeste.TimeRate = 1;
                Celeste.DashAssistFreeze = false;
                Celeste.OverloadGameLoop = null;

                f_Engine_nextScene.SetValue(Celeste.Instance, newScene);

                try {
                    Celeste.Scene?.End();
                } catch (Exception ex) {
                    Logger.Log(LogLevel.Error, "crit-error-handler", "Error executing scene End function on immediate scene switch:");
                    Logger.LogDetailed(ex, "crit-error-handler");
                }

                f_Engine_scene.SetValue(Celeste.Instance, newScene);

                try {
                    Celeste.Scene?.Begin();
                } catch (Exception ex) {
                    Logger.Log(LogLevel.Error, "crit-error-handler", "Error executing scene Begin function on immediate scene switch:");
                    Logger.LogDetailed(ex, "crit-error-handler");
                }
            }

            if (CurrentHandler.State < DisplayState.CleanScene) {
                Scene oldScene = CurrentHandler.Scene;
                CurrentHandler.RemoveSelf();
                oldScene?.Entities?.UpdateLists();

                Scene errorScene = new Scene();
                errorScene.Add(new HudRenderer());
                errorScene.Add(new HiresSnow());
                errorScene.Add(CurrentHandler);
                CurrentHandler.State = DisplayState.CleanScene;
                errorScene.Entities.UpdateLists();

                DoImmediateSceneSwitch(errorScene);
                return null;
            }

            // If we aren't on a bluescreen scene, try that as a last resort
            if (CurrentHandler.State < DisplayState.BlueScreen) {
                Scene oldScene = CurrentHandler.Scene;
                CurrentHandler.RemoveSelf();
                oldScene?.Entities?.UpdateLists();

                Scene blueScreenScene = new Scene();
                blueScreenScene.Add(new HudRenderer());
                blueScreenScene.Add(CurrentHandler);
                CurrentHandler.State = DisplayState.BlueScreen;
                blueScreenScene.Entities.UpdateLists();
    
                DoImmediateSceneSwitch(blueScreenScene);
                return null;
            }

            // The game is too instable to continue - rethrow the original error
            return CurrentHandler.Error;
        }

        private static void ResetErrorHandler() {
            Logger.Log(LogLevel.Info, "crit-error-handler", "Resetting critical error handler");
            CurrentHandler?.Dispose();
            CurrentHandler?.OnClose?.Invoke();
            CurrentHandler = null;
        }

        private static string BackupLogFile(Exception error, out string logFileError) {
            // Determine the log file path
            if (string.IsNullOrEmpty(Everest.PathLog)) {
                logFileError = "<no log file has been written>";
                return null;
            }

            // Choose a backup file name
            string backupBaseName = $"log_{DateTime.Now:yyyyMMdd_HHmmss}";
            string backupPath = Path.Combine(Everest.PathGame, "CrashLogs", $"{backupBaseName}.txt");
            for (int idx = 2; File.Exists(backupPath); idx++)
                backupPath = Path.Combine(Path.GetDirectoryName(backupPath), $"{backupBaseName}_{idx}.txt");

            // Backup the log file
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                File.Copy(Everest.PathLog, backupPath);

                // Add some additional info to the end
                static string EvalSafe(Func<string> func) {
                    try {
                        return func();
                    } catch (Exception e) {
                        return $"<error during evaluation: {e.GetType().FullName}: {e.Message}>";
                    }
                }

                using StreamWriter writer = new StreamWriter(backupPath, true);
                writer.WriteLine();
                writer.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> END OF BACKED UP LOG FILE <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
                writer.WriteLine();

                writer.WriteLine($"Celeste/Everest Version: {Everest.VersionCelesteString}");
                writer.WriteLine($"Operating System: {RuntimeInformation.OSDescription} [{RuntimeInformation.RuntimeIdentifier}]");
                writer.WriteLine($".NET Runtime Description: {RuntimeInformation.FrameworkDescription}");

                try {
                    PresentationParameters presentParams = Celeste.Graphics.GraphicsDevice?.PresentationParameters;
                    if (presentParams != null)
                        writer.WriteLine($"Presentation Parameters: {presentParams.BackBufferWidth}x{presentParams.BackBufferHeight} {Enum.GetName<SurfaceFormat>(presentParams.BackBufferFormat)} {(presentParams.IsFullScreen ? "fullscreen" : "windowed")} {(presentParams.PresentationInterval > 0 ? "vsync" : "no vsync")}");

                    GraphicsAdapter adapter = Celeste.Graphics.GraphicsDevice.Adapter;
                    if (adapter != null)
                        writer.WriteLine($"Graphics Adapater: '{adapter.DeviceName}' [{adapter.Description}] {adapter.CurrentDisplayMode.Width}x{adapter.CurrentDisplayMode.Height} {Enum.GetName<SurfaceFormat>(adapter.CurrentDisplayMode.Format)}");
                } catch (Exception ex) {
                    writer.WriteLine($"Error dumping graphics settings: {ex.GetType().FullName}: {ex.Message}");
                }

                writer.WriteLine();

                static string FormatByteCount(long bytes) {
                    switch (bytes) {
                        case >= 1024L*1024L*1024L*1024L: return $"{bytes / (1024L*1024L*1024L) / 1024f}TB"; // This should never happen, but just to be future proof .-.
                        case >= 1024L*1024L*1024L: return $"{bytes / (1024L*1024) / 1024f}GB"; 
                        case >= 1024L*1024L: return $"{bytes / (1024L) / 1024f}MB"; 
                        case >= 1024L: return $"{bytes / 1024f}KB"; 
                        default: return $"{bytes}B";
                    }
                }

                GCMemoryInfo memInfo = GC.GetGCMemoryInfo();
                Process procSelf = Process.GetCurrentProcess();
                writer.WriteLine($"Managed Heap Size: {EvalSafe(() => $"{FormatByteCount(memInfo.HeapSizeBytes)} (committed: {FormatByteCount(memInfo.TotalCommittedBytes)})")}");
                writer.WriteLine($"Total Memory Usage: {EvalSafe(() => $"private={FormatByteCount(procSelf.PrivateMemorySize64)} system={FormatByteCount(procSelf.PagedSystemMemorySize64 + procSelf.NonpagedSystemMemorySize64)} virtual={FormatByteCount(procSelf.VirtualMemorySize64)} physical={FormatByteCount(procSelf.WorkingSet64)} paged={FormatByteCount(procSelf.PagedMemorySize64)}")}");
                writer.WriteLine($"Peak Memory Usage: {EvalSafe(() => $"virtual={FormatByteCount(procSelf.VirtualMemorySize64)} physical={FormatByteCount(procSelf.PeakWorkingSet64)} paged={FormatByteCount(procSelf.PeakPagedMemorySize64)}")}");
                writer.WriteLine($"Available Memory: {EvalSafe(() => FormatByteCount(memInfo.TotalAvailableMemoryBytes))}");
                writer.WriteLine();

                writer.WriteLine("Loaded Mods");
                try {
                    lock (Everest._Modules) {
                        bool updatesAvailable = ModUpdaterHelper.IsAsyncUpdateCheckingDone();
                        Dictionary<EverestModuleMetadata, ModUpdateInfo> availableUpdatesInv = new();
                        if (updatesAvailable) {
                            try {
                                SortedDictionary<ModUpdateInfo, EverestModuleMetadata> availableUpdates =
                                    ModUpdaterHelper.GetAsyncLoadedModUpdates();
                                foreach ((ModUpdateInfo key, EverestModuleMetadata value) in availableUpdates) {
                                    availableUpdatesInv[value] = key;
                                }

                                writer.WriteLine(
                                    "(entries marked with * are known to not match latest on the database)");
                            } catch (Exception ex) {
                                writer.WriteLine($"(failed to check update status: {ex.GetType().FullName}: {ex.Message})");
                                updatesAvailable = false;
                            }
                        }
                        
                        foreach (EverestModule mod in Everest._Modules) {
                            writer.Write($" - {mod.Metadata.Name}: ");
                            writer.Write($"{mod.Metadata.VersionString}");
                            if (updatesAvailable && availableUpdatesInv.TryGetValue(mod.Metadata, out ModUpdateInfo updateInfo)) {
                                writer.Write($"* (-> {updateInfo.Version})");
                            }
                            writer.Write($"{mod switch { NullModule => "", _ => $" [{mod.GetType().FullName}]" }}");
                            writer.WriteLine();
                        }
                    }
                } catch (Exception ex) {
                    writer.WriteLine($" - error listing mods: {ex.GetType().FullName}: {ex.Message}");
                }

                writer.WriteLine();
                writer.WriteLine($"Crash Exception: {error}");
                writer.WriteLine();
                writer.WriteLine($"Crash HResult: {error.HResult}");
                writer.WriteLine($"Inner exception (if any): {error.InnerException}");
                writer.WriteLine();
                
                StackTrace trace = new(error, true);
                StackFrame latestFrame = trace.GetFrame(0);
                if (latestFrame != null) {
                    writer.WriteLine($"Last stack frame: {latestFrame}");
                    writer.WriteLine($"Frame IL offset: {latestFrame.GetILOffset()}");
                    writer.WriteLine($"Frame native offset: {latestFrame.GetNativeOffset()}");
                } else {
                    writer.WriteLine("Couldn't fetch latest frame!");
                }
            } catch (Exception ex) {
                Logger.Log(LogLevel.Error, "crit-error-handler", "Error backing up log file:");
                Logger.LogDetailed(ex, "crit-error-handler");
                logFileError = $"<error backing up log file: {ex.Message}>";
                return null;
            }

            Logger.Log(LogLevel.Info, "crit-error-handler", $"Backed up log file to '{backupPath}'");
            logFileError = null;
            return backupPath;
        }

        private static void AmendLogFile(string logFile, string descr, Exception error) {
            try {
                using StreamWriter writer = new StreamWriter(logFile, true);
                writer.WriteLine();
                writer.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> AMENDED INFORMATION <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
                writer.WriteLine();
                writer.WriteLine($"Encountered an additional error after the initial crash: {descr}");
                writer.WriteLine("!!!!!!!!!!!!!!!!!!!! THIS IS NOT THE MAIN CRASH! !!!!!!!!!!!!!!!!!!!!");
                writer.WriteLine($"Exception: {error}");

                Logger.Log(LogLevel.Info, "crit-error-handler", $"Amended backed up log file '{logFile}' after encountering an additional error after the initial crash");
            } catch (Exception ex) {
                Logger.Log(LogLevel.Error, "crit-error-handler", "Error amending log file:");
                Logger.LogDetailed(ex, "crit-error-handler");
            }
        }

        public readonly ExceptionDispatchInfo Error;
        private readonly string errorType, errorMessage, errorStackTrace;
        public readonly string LogFile, LogFileError;
        public readonly Session Session;

        public bool EncounteredAdditionalErrors { get; private set; }
        private readonly BeforeRenderInterceptor beforeRenderInterceptor;
        private TextMenu optMenu;
        private readonly HashSet<UserChoice> failedChoices = new HashSet<UserChoice>();
        private bool hasFlushedSaveData;

        private DisplayState state = DisplayState.Initial;
        public DisplayState State {
            get => state;
            private set {
                state = value;
                if (optMenu != null)
                    ConfigureOptionsMenu();                
            }
        }

        public event Action OnClose;

        private bool disablePlayerSprite;
        private PlayerSprite playerSprite;
        private PlayerHair playerHair;
        private VirtualRenderTarget playerRenderTarget;
        private bool UsePlayerSprite => !disablePlayerSprite && State != DisplayState.BlueScreen;

        private bool playerShouldTeabag;
        private bool isCrouched;
        private float crouchTimer;

        private CriticalErrorHandler(ExceptionDispatchInfo error, string logFile, string logFileError) {
            Depth += 100; // Render below other overlays
            Session = (Celeste.Scene as Level)?.Session;

            Error = error;
            errorType = error.SourceException.GetType().FullName;
            errorMessage = error.SourceException.Message;
            if (error.SourceException is AggregateException aggrEx)
                // At least report the first stack trace
                errorStackTrace = aggrEx.InnerException.StackTrace;
            else
                errorStackTrace = error.SourceException.StackTrace;

            LogFile = logFile;
            LogFileError = string.IsNullOrEmpty(logFile) ? logFileError : null;

            playerShouldTeabag = CoreModule.Settings.CrashHandlerAlwaysTeabag || (!(Settings.Instance?.DisableFlashes ?? true) && new Random().Next(0, 10) == 0);

            beforeRenderInterceptor = new BeforeRenderInterceptor(BeforeRender);
            Add(new Coroutine(Routine()));
            Logger.Log(LogLevel.Info, "crit-error-handler", $"Created critical error handler for exception {errorType}: {errorMessage}");
        }

        public void Dispose() {
            playerRenderTarget?.Dispose();
            playerRenderTarget = null;
        }

        private IEnumerator Routine() {
            retry:;
            if (State != DisplayState.BlueScreen)
                yield return FadeIn();
            else
                Fade = 1f;

            // Create the options menu
            optMenu = new TextMenu() { AutoScroll = false };

            optMenu.Add(new TextMenu.Button("Open log file folder") { Disabled = LogFileError != null }.Pressed(() => {
                string openProg =
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "explorer.exe" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "xdg-open" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" :
                    throw new NotSupportedException();

                Process.Start(new ProcessStartInfo() {
                   FileName = openProg,
                   ArgumentList = { Path.GetDirectoryName(LogFile) },
                   UseShellExecute = true 
                });
            }));

            UserChoice? choice = null;
            if (Session != null) {
                optMenu.Add(new TextMenu.Button("Retry level") { Disabled = !CanExecuteChoice(UserChoice.RetryLevel) }.Pressed(() => choice = UserChoice.RetryLevel));
                optMenu.Add(new TextMenu.Button("Save & Quit") { Disabled = !CanExecuteChoice(UserChoice.SaveAndQuit) }.Pressed(() => choice = UserChoice.SaveAndQuit));
            }

            if (SaveData.Instance != null && !hasFlushedSaveData)
                optMenu.Add(new TextMenu.Button("Save current progress") { Disabled = !CanExecuteChoice(UserChoice.FlushSaveData) }.Pressed(() => choice = UserChoice.FlushSaveData));

            optMenu.Add(new TextMenu.Button("Return to main menu") { Disabled = !CanExecuteChoice(UserChoice.ReturnToMainMenu) }.Pressed(() => choice = UserChoice.ReturnToMainMenu));

            optMenu.Add(new TextMenu.Button("Exit Game").Pressed(() => {
                Scene.OnEndOfFrame += static () => Engine.Instance.Exit();
            }));
            optMenu.Add(new TextMenu.Button("Restart Game").Pressed(() => {
                Everest.Events.Celeste.OnShutdown += static () => BOOT.StartCelesteProcess();
                Scene.OnEndOfFrame += static () => Engine.Instance.Exit();
            }));

            ConfigureOptionsMenu();

            // Wait for the user to make a choice, or the scene to change, or the display state to change
            Scene prevScene = Celeste.Scene;
            DisplayState prevState = State;

            while (choice == null && prevScene == Celeste.Scene && State == prevState)
                yield return null;

            if (prevScene != Celeste.Scene || prevState != State)
                goto retry;

            // Fade out the menu
            if (State != DisplayState.BlueScreen)
                yield return FadeOut();

            // Execute the choice
            if (!ExecuteUserChoice(choice.Value)) {
                failedChoices.Add(choice.Value);
                goto retry;
            }
            if (CurrentHandler == this)
                goto retry;

            RemoveSelf();
        }

        private bool CanExecuteChoice(UserChoice choice) => !failedChoices.Contains(choice) && choice switch {
            UserChoice.FlushSaveData => SaveData.Instance != null && !hasFlushedSaveData,
            UserChoice.RetryLevel => Session != null,
            UserChoice.SaveAndQuit => SaveData.Instance != null && Session != null,
            UserChoice.ReturnToMainMenu => true,
            _ => false
        };

        private bool ExecuteUserChoice(UserChoice choice) {
            Logger.Log(LogLevel.Info, "crit-error-handler", $"Executing user choice {Enum.GetName<UserChoice>(choice)}");
            try {
                SaveData save = SaveData.Instance;
                switch (choice) {
                    case UserChoice.FlushSaveData:
                        if (save != null) {
                            save.BeforeSave();
                            UserIO.Save<SaveData>(SaveData.GetFilename(save.FileSlot), UserIO.Serialize(save));
                            patch_UserIO.ForceSerializeModSave();
                        }
                        hasFlushedSaveData = true;
                        return true; // We don't want to reset the overlay yet

                    case UserChoice.RetryLevel:
                        if (Session == null) {
                            Logger.Log(LogLevel.Warn, "crit-error-handler", "Can't retry as no session is present!");
                            return false;
                        }

                        Celeste.Scene = new LevelLoader(Session);
                        break;

                    case UserChoice.SaveAndQuit:
                        if (Session == null || save == null) {
                            Logger.Log(LogLevel.Warn, "crit-error-handler", "Can't save-and-quit as either session or save data is not present!");
                            return false;
                        }

                        // Do a save manually
                        Session.InArea = true;
                        save.BeforeSave();
                        UserIO.Save<SaveData>(SaveData.GetFilename(save.FileSlot), UserIO.Serialize(save));
                        patch_UserIO.ForceSerializeModSave();

                        // Load the overworld
                        Celeste.Scene = new OverworldLoader(Overworld.StartMode.MainMenu);
                        break;

                    case UserChoice.ReturnToMainMenu:
                        Celeste.Scene = new OverworldLoader(Overworld.StartMode.MainMenu);
                        break;
                }
            } catch (Exception ex) {
                Logger.Log(LogLevel.Error, "crit-error-handler", $"Error while executing user choice:");
                Logger.LogDetailed(ex, "crit-error-handler");
                AmendLogFile(LogFile, $"Error while executing user choice {Enum.GetName<UserChoice>(choice)}", ex);
                return false;
            }

            // Reset the overlay
            ResetErrorHandler();
            return true;
        }

        private void ConfigureOptionsMenu() {
            optMenu.ItemSpacing = 4;
            optMenu.RecalculateSize();

            if (UsePlayerSprite) {
                optMenu.Position = new Vector2(Celeste.TargetWidth * 0.15f, Celeste.TargetHeight * 0.55f);
                optMenu.Justify = new Vector2(0.5f, 0f);

                // Reduce item spacing if there are too many items
                if (optMenu.Position.Y + optMenu.Height > Celeste.TargetHeight * 0.85f) {
                    optMenu.ItemSpacing = 0;
                    optMenu.RecalculateSize();
                }
            } else {
                optMenu.Position = new Vector2(Celeste.TargetWidth * 0.15f, Celeste.TargetHeight * 0.6f);
                optMenu.Justify = new Vector2(0.5f, 0.5f);
            }
        }

        public override void Added(Scene scene) {
            Overlay oldOverlay = (scene as IOverlayHandler)?.Overlay;

            base.Added(scene);
            scene.Add(beforeRenderInterceptor);

            // Preserve the old overlay
            if (oldOverlay != null)
                ((IOverlayHandler) scene).Overlay = oldOverlay;
        }

        public override void Removed(Scene scene) {
            scene.Remove(beforeRenderInterceptor);
            base.Removed(scene);
        }

        public override void SceneEnd(Scene scene) {
            // Transfer over to the new scene
            Scene newScene = f_Engine_nextScene.GetValue(Celeste.Instance) as Scene;
            if (newScene != null && scene != null && CurrentHandler == this) {
                scene.Remove(this);
                newScene.Add(this);
            }

            base.SceneEnd(scene);
        }

        public override void Update() {
            // Check if another overlay is active
            if (Scene is IOverlayHandler ovlHandler && ovlHandler.Overlay != this) {
                if (ovlHandler.Overlay != null)
                    return;
                ovlHandler.Overlay = this; // Restore ourselves as the active overlay
            }

            // Update the player state
            if (UsePlayerSprite && playerSprite != null && playerHair != null) {
                if (playerShouldTeabag) {
                    if (playerSprite.CurrentAnimationID != "rollGetUp") {
                        // Make them teabag so that the situation is a bit less dramatic
                        // This is not the default behavior because people complained that it was too distracting ._.
                        crouchTimer -= Celeste.DeltaTime;
                        if (crouchTimer <= 0) {
                            playerSprite.Scale = isCrouched ? new Vector2(0.8f, 1.2f) : new Vector2(1.4f, 0.6f);
                            isCrouched = !isCrouched;
                            crouchTimer = 0.12f;
                        }
                        playerSprite.Play(isCrouched ? "duck" : "idle");
                    }
                } else {
                    // Boring fall animation ._.
                    if (playerSprite.LastAnimationID != "roll")
                        playerSprite.Play("roll");

                    // If the player holds down left or right for 5s switch to teabagging
                    // Recycle crouchTimer to keep track of this
                    if (Input.MoveX.Value != 0 || Input.MenuLeft.Check || Input.MenuRight.Check)
                        crouchTimer += Celeste.DeltaTime;
                    else
                        crouchTimer = 0;

                    if (crouchTimer >= 5) {
                        // Play the get-up animation first
                        playerSprite.Play("rollGetUp");
                        playerShouldTeabag = true;
                        crouchTimer = 0;   
                    }
                }

                playerSprite.Update();
                playerHair.Update();
                if (playerSprite != null) {
                    playerSprite.Scale.X = Calc.Approach(playerSprite.Scale.X, 1, 1.75f * Celeste.DeltaTime);
                    playerSprite.Scale.Y = Calc.Approach(playerSprite.Scale.Y, 1, 1.75f * Celeste.DeltaTime);
                }
            }

            // Update the options menu
            if (Fade == 1)
                optMenu?.Update();

            base.Update();
        }

        private void BeforeRender() {
            if (UsePlayerSprite) {
                try {
                    playerSprite ??= new PlayerSprite(PlayerSpriteMode.MadelineNoBackpack) { RenderPosition = Vector2.Zero };
                    playerHair ??= new PlayerHair(playerSprite) { Facing = Facings.Right, SimulateMotion = true, StepPerSegment = Vector2.UnitY * 2, StepInFacingPerSegment = 0.5f, StepApproach = 64f, StepYSinePerSegment = 0f };
                    playerRenderTarget ??= VirtualContent.CreateRenderTarget("crit-error-handler-player", 32, 32);

                    // Draw the player sprite to the render target
                    Celeste.Instance.GraphicsDevice.SetRenderTarget(playerRenderTarget);
                    Celeste.Instance.GraphicsDevice.Clear(Color.Transparent);

                    SpriteSortMode spriteSortMode = SpriteSortMode.Deferred;
                    BlendState blendState = BlendState.AlphaBlend;
                    SamplerState samplerState = SamplerState.PointClamp;
                    DepthStencilState depthStencilState = DepthStencilState.Default;
                    RasterizerState rasterizerState = RasterizerState.CullNone;
                    Effect effect = null;
                    Matrix matrix = Matrix.CreateTranslation(16, 32, 0);
                    OnBeforePlayerRender?.Invoke(ref spriteSortMode, ref blendState, ref samplerState, ref depthStencilState, ref rasterizerState, ref effect, ref matrix);
                    Draw.SpriteBatch.Begin(spriteSortMode, blendState, samplerState, depthStencilState, rasterizerState, effect, matrix);

                    try {
                        playerHair.AfterUpdate();
                        playerHair.Render();
                        playerSprite.Render();
                    } finally {
                        Draw.SpriteBatch.End();
                        OnAfterPlayerRender?.Invoke();
                    }
                } catch (Exception ex) {
                    Logger.Log(LogLevel.Error, "crit-error-handler", "Error while rendering player sprite:");
                    Logger.LogDetailed(ex, "crit-error-handler");
                    AmendLogFile(LogFile, "Error while rendering player sprite", ex);
                    disablePlayerSprite = true;
                }
            }
        }

        /// <summary>
        /// This event is invoked before the player sprite is being rendered to
        /// its render target. Handlers may modify the sprite batch state which
        /// is used for drawing the sprite. When invoked, there's no active
        /// sprite batch, but the render target has already been bound and
        /// cleared.
        /// </summary>
        public static event BeforePlayerRenderHandler OnBeforePlayerRender;
        public delegate void BeforePlayerRenderHandler(ref SpriteSortMode sortMode, ref BlendState blendState, ref SamplerState samplerState, ref DepthStencilState depthStencilState, ref RasterizerState rasterizerState, ref Effect effect, ref Matrix matrix);

        /// <summary>
        /// This event is invoked after the player sprite has been rendered to
        /// its render target. When invoked, the sprite batch has already been
        /// ended.
        /// </summary>
        public static event Action OnAfterPlayerRender;

        public override void Render() {
            // Draw the background
            switch (State) {
                case DisplayState.Overlay:
                    RenderFade();
                    break;

                case DisplayState.BlueScreen:
                    Draw.Rect(-10, -10, Celeste.TargetWidth + 20, Celeste.TargetHeight + 20, new Color(0x20, 0x40, 0x60));
                    break;
            }

            // Draw the options menu
            if (optMenu != null) {
                optMenu.Alpha = Fade;
                optMenu.Render();

                if (failedChoices.Count > 0)
                    ActiveFont.Draw("Failed to execute user action", optMenu.Position - Vector2.UnitY * (optMenu.Height * optMenu.Justify.Y + 5), new Vector2(0.5f, 1), new Vector2(0.7f), Color.IndianRed * Fade);
            }

            // Draw the player render target to the screen
            if (UsePlayerSprite && playerRenderTarget != null) {
                HudRenderer.EndRender();
                try {
                    HudRenderer.BeginRender(sampler: SamplerState.PointClamp);
                    try {
                        Vector2 drawPos = new Vector2(Celeste.TargetWidth * 0.15f, Celeste.TargetHeight * 0.5f);
                        float size = Celeste.TargetHeight * 0.65f;
                        Draw.SpriteBatch.Draw((RenderTarget2D) playerRenderTarget, new Rectangle((int) (drawPos.X - size / 2), (int) (drawPos.Y - size), (int) size, (int) size), Color.White * Fade);
                    } finally {
                        HudRenderer.EndRender();
                    }
                } catch (Exception ex) {
                    Logger.Log(LogLevel.Error, "crit-error-handler", "Error while rendering player sprite:");
                    Logger.LogDetailed(ex, "crit-error-handler");
                    AmendLogFile(LogFile, "Error while rendering player sprite", ex);
                    disablePlayerSprite = true;
                } finally {
                    HudRenderer.BeginRender();
                }
            }

            // Draw the error UI
            Vector2 textPos = new Vector2(Celeste.TargetWidth * 0.3f, Celeste.TargetHeight * 0.35f);
            ActiveFont.Draw("Oooops! :(", textPos, new Vector2(0, 1), new Vector2(3), Color.White * Fade);
            textPos.X += 50;

            void DrawLineWrap(string text, float scale, Color color, Vector2 posOff = default) {
                bool firstLine = true;
                while (firstLine || !string.IsNullOrWhiteSpace(text)) {
                    // Handle line breaking
                    int lineLen;
                    float availSpace = Celeste.TargetWidth * 0.95f - (textPos.X + posOff.X);
                    if (ActiveFont.Measure(text).X * scale > availSpace) {
                        // Do binary search to determine the cutoff point
                        int start = 0, end = text.Length;
                        while (start < end -1) {
                            int middle = start + (end - start) / 2;
                            float textSize = ActiveFont.Measure(text.Substring(0, middle)).X * scale;
                            if (textSize > availSpace)
                                end = middle;
                            else
                                start = middle;
                        }
                        lineLen = start;
                    } else
                        lineLen = text.Length;

                    // Draw one line, and advance to the text
                    ActiveFont.Draw(text.Substring(0, lineLen), textPos + posOff, Vector2.Zero, new Vector2(scale), color * Fade);
                    textPos.Y += ActiveFont.LineHeight * 1.1f * scale;

                    text = text.Substring(lineLen);
                    if (firstLine)
                        posOff.X += ActiveFont.LineHeight * 0.8f * scale;
                    firstLine = false;
                }
            }

            DrawLineWrap("Celeste/Everest encountered a critical error.", 0.7f, Color.LightGray);
            DrawLineWrap("Please report this in the Celeste discord!", 0.7f, Color.LightGray);
            DrawLineWrap("discord.gg/celeste - channel #modding_help", 0.5f, Color.Gray, Vector2.UnitX * 50);
            DrawLineWrap("Your log file has been backed up; please attach it to your bug report.", 0.7f, Color.LightGray);
            if (string.IsNullOrEmpty(LogFileError))
                DrawLineWrap(LogFile, 0.4f, Color.Gray, Vector2.UnitX * 50);
            else
                DrawLineWrap(LogFileError, 0.5f, Color.OrangeRed, Vector2.UnitX * 50);

            if (EncounteredAdditionalErrors) {
                textPos.Y += 20;
                DrawLineWrap("Additional errors have occurred since the initial crash!", 0.7f, Color.IndianRed);
            }

            textPos.Y += 20;

            DrawLineWrap($"Error Details: {errorType}: {errorMessage}", 0.7f, Color.LightGray);
            textPos.X += 50;
            string[] btLines = (errorStackTrace ?? string.Empty)
                .Split('\n')
                .Select(l => l.Trim())
                .ToArray();
            for (int i = 0; i < btLines.Length; i++) {
                // Declutter the stack trace from MonoMod detours, additionally skip the check if it's the latest one,
                // since that means the crash was in there
                if (i != 0 && btLines[i].StartsWith("at Hook<")) continue;
                DrawLineWrap(btLines[i], 0.4f, Color.Gray);
                if (textPos.Y >= Celeste.TargetHeight * 0.9f && i+1 < btLines.Length) {
                    DrawLineWrap("...", 0.5f, Color.Gray);
                    break;
                }
            }
        }

    }
}