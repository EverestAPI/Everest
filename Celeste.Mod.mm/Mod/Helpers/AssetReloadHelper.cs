using Celeste.Mod.Helpers;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public class AssetReloadHelper : Scene, IDisposable {

        public static readonly HashSet<string> SilentThreadList = new HashSet<string>() {
            "Main Thread",
            "GAME_LOADER",
            "OVERWORLD_LOADER",
            "SUMMIT_VIGNETTE",
            "LEVEL_LOADER",
            "COMPLETE_LEVEL",
            "Wave Dash Presentation Loading",
        };

        private static readonly SemaphoreSlim ReloadSemaphore = new SemaphoreSlim(1, 1); // Held when any thread is executing a [non-]silent reload

        private static readonly object _CurrentHelperSceneLock = new object(); // Protects CurrentHelperScene so that only one thread may create a new reload helper scene
        private static volatile Task<AssetReloadHelper> CurrentHelperScene; // A task which yields the current helper scene, or null if there is no such scene

        public static void Do(string text, Action reload = null, Action done = null) => Do(false, text, reload, done);
        public static void Do(bool silent, string text, Action reload = null, Action done = null) => Do(text, silent => {
            reload?.Invoke();

            // Immediately execute the done callback if we're silently reloading
            if (silent) {
                done?.Invoke();
                return Task.CompletedTask;
            }

            // Execute the done callback on the main thread
            if (done != null)
                return MainThreadHelper.Schedule(done).AsTask();
            else
                return Task.CompletedTask;
        }, silent, useWorkerThread: reload != null);

        public static Task Do(string text, Func<bool, Task> action, bool silent = false, bool useWorkerThread = true) {
            // Check if the crash handler is currently active
            if (CriticalErrorHandler.CurrentHandler != null) {
                Logger.Log(LogLevel.Warn, "reload", $"Delaying reload action until critical error handler is closed: {text}");
                return DelayReloadUntilAfterCrash(silent, text, action, useWorkerThread);
            }

            if (Celeste.LoadTimer != null)
                // Silently reload if we are still loading the game
                silent = true;
            else if (silent) {
                // Check if this thread is a loading thread, and as such allowed to silently reload
                string name = Thread.CurrentThread.Name ?? "<null>";
                if (!SilentThreadList.Contains(name)) {
                    Logger.Log(LogLevel.Warn, "reload", $"Tried to silently reload on non-whitelisted thread {name}: {text}");
                    silent = false;
                }
            }

            // If we should silently reload, just immediately execute the action
            if (silent) {
                return ((Func<Task>) (async () => {
                    // Grab the reload semaphore to ensure that there are no two simultaneous reloads
                    await ReloadSemaphore.WaitAsync();
                    try {
                        Logger.Log(LogLevel.Verbose, "reload", $"Starting execution of silent reload action: {text}");
                        await action(true);
                        Logger.Log(LogLevel.Verbose, "reload", $"Finished execution of silent reload action: {text}");
                    } finally {
                        ReloadSemaphore.Release();
                    }
                }))();
            }

            // If there's no scene, wait until there is a scene
            if (Engine.Scene == null) {
                return MainThreadHelper.Schedule(async () => {
                    while (Engine.Scene == null)
                        await MainThreadHelper.YieldFrame;

                    return Do(text, action, useWorkerThread: useWorkerThread);
                }).Unwrap();
            }

            lock (_CurrentHelperSceneLock) {
                // Create a new reload helper in case there currently isn't one
                // We have to create it on the main thread as otherwise accessing the current scene is not safe
                CurrentHelperScene ??= MainThreadHelper.Schedule(StartReloadScene);

                // Add our action to the helper's task queue
                return CurrentHelperScene.ContinueWith(t => {
                    AssetReloadHelper helper = t.Result;

                    // Prepare the task start function
                    Func<Task> taskFnc;
                    if (useWorkerThread)
                        taskFnc = () => helper.workerTaskFactory.StartNew(() => action(false)).Unwrap();
                    else
                        taskFnc = () => action(false);

                    // Enqueue the reload action onto the task queue
                    // Use a TaskCompletionSource to also capture the task for the caller of this function
                    TaskCompletionSource<Task> complSrc = new TaskCompletionSource<Task>();
                    CancellationTokenRegistration cancelReg = helper.CancellationToken.Register(complSrc.SetCanceled);

                    helper.taskQueue.Enqueue((text, () => {
                        // Start the task
                        Task task = taskFnc();

                        // Set the completion source result so that the reload initiator can also await it
                        cancelReg.Dispose();
                        complSrc.SetResult(task);

                        return task;
                    }));

                    Logger.Log(LogLevel.Verbose, "reload", $"Queued non-silent reload action: {text}");

                    return complSrc.Task.Unwrap();
                }).Unwrap();
            }
        }

        private static Task DelayReloadUntilAfterCrash(bool silent, string text, Func<bool, Task> action, bool useWorkerThread) {
            // If we are silent but not on the main thread, block that thread until the crash handler closes
            // This ensures that we still instantly execute the reload action
            if (silent && !MainThreadHelper.IsMainThread) {
                using ManualResetEventSlim evt = new ManualResetEventSlim(false);

                MainThreadHelper.Schedule(() => {
                    if (CriticalErrorHandler.CurrentHandler == null)
                        // The handler was closed while we were queuing on the main thread
                        evt.Set();
                    else
                        // Unblock once the error handler closes
                        CriticalErrorHandler.CurrentHandler.OnClose += evt.Set;
                });

                evt.Wait();
                return Do(text, action, true, useWorkerThread);
            } 

            // If we are silent and are on the main thread, we can't block until the crash handler closes
            // As such we can't immediately execute the callbacks, which *might* break some caller code
            // Log a warning and hope it didn't depend on us instantly executing ._.
            if (silent && MainThreadHelper.IsMainThread)
                Logger.Log(LogLevel.Warn, "reload", $" - can't immediately execute the action silently because we are on the main thread; this might break caller logic!");

            // Asynchronously wait for the crash handler to close, then run the action
            TaskCompletionSource<Task> complSrc = new TaskCompletionSource<Task>();
            MainThreadHelper.Schedule(() => {
                if (CriticalErrorHandler.CurrentHandler == null)
                    // The handler was closed while we were queuing on the main thread
                    complSrc.SetResult(Do(text, action, silent, useWorkerThread));
                else
                    // Execute the action once the error handler closes
                    CriticalErrorHandler.CurrentHandler.OnClose += () => complSrc.SetResult(Do(text, action, silent, useWorkerThread));
            });
            return complSrc.Task.Unwrap();
        }

        private static readonly object levelReloadLock = new object();
        private static Task curLevelReload;

        public static void ReloadLevel() => ReloadLevel(false);
        public static Task ReloadLevel(bool forceReload = false) {
            // Don't reload the level if we are currently still loading the game
            if (Celeste.LoadTimer != null)
                return Task.CompletedTask;

            // Don't start another reload if there already is one in progress
            if (!forceReload) {
                lock (levelReloadLock) {
                    if (curLevelReload?.IsCompleted ?? false)
                        curLevelReload = null;

                    curLevelReload ??= ReloadLevel(forceReload: true);
                    return curLevelReload;
                }
            }

            return MainThreadHelper.Schedule(() => {
                // Check if we are currently in a level
                Level lvl = Engine.Scene as Level;
                if (lvl == null && IsReloading)
                    lvl = ReturnToScene as Level;

                if (lvl == null)
                    return Task.CompletedTask;

                Logger.Log(LogLevel.Info, "reload", "Starting level reload...");

                // Start the reload action
                return Do(Dialog.Clean("ASSETRELOADHELPER_RELOADINGLEVEL"), async _ => {
                    try {
                        patch_Level.LoadOverride loadOvr = new patch_Level.LoadOverride();

                        // If we're transitioning, adjust the spawnpoint
                        if (lvl.Transitioning) {
                            lvl.Session.RespawnPoint = lvl.GetSpawnPoint(lvl.Tracker?.GetEntity<Player>()?.Position ?? Vector2.Zero);
                        }

                        // Start a new level loader while preserving the old session and respawn point
                        LevelLoader loader = new LevelLoader(lvl.Session, lvl.Session.RespawnPoint);
                        ReturnToScene = loader;

                        // Preserve the level state
                        loadOvr.ShouldAutoPause = lvl.Paused;

                        // Preserve the player state
                        Player player = lvl.Tracker?.GetEntity<Player>();
                        if (player != null && !player.Dead) {
                            // Skip the intro screen wipe, and keep the old player entity
                            loadOvr.SkipScreenWipes++;
                            loadOvr.NextLoadedPlayer = player;
                            ((patch_Player) player).OverrideIntroType = Player.IntroTypes.Transition;

                            // Ensure the player is not a dummy or invisible
                            player.StateMachine.Locked = false;
                            player.StateMachine.State = Player.StNormal;
                            player.Sprite.Visible = player.Hair.Visible = true;

                            // Detach the player from the old level
                            player.Light.Index = -1;
                            player.Leader.LoseFollowers();
                            player.Holding?.Release(Vector2.Zero);
                            player.Holding = null;
                        }

                        // Wait until the level finished loading
                        while (!loader.Loaded)
                            await Task.Delay(50);

                        // Apply the load overrides if the load didn't fail
                        patch_Level.RegisterLoadOverride(loader.Level, loadOvr);
                    } catch (Exception ex) {
                        string sid = lvl.Session?.Area.GetSID() ?? "NULL";

                        Logger.Log(LogLevel.Warn, "reload", $"Failed reloading level '{sid}':");
                        Logger.LogDetailed(ex, "reload");

                        // Open an error postcard
                        patch_LevelEnter.ErrorMessage = Dialog.Get("postcard_levelloadfailed").Replace("((sid))", sid);
                        ReturnToScene = patch_LevelEnter.ForceCreate(lvl.Session, false); // We don't know if we managed to create the LevelLoader 
                    }
                });
            });
        }

        internal static object AreaReloadLock = new object();
        public static void ReloadAllMaps() {
            // Prevent anything from calling AreaData.Get while we are reloading
            lock (AreaReloadLock) {
                SaveData saveData = SaveData.Instance;
                
                // ChapterSelect only updates the ID.
                // Note: SaveData.Instance.LastArea is reset by AreaData.Interlude_Safe -> SaveData.LevelSetStats realizing that AreaOffset == -1
                // Store the "resolved" last selected area in a local variable, then re-set it after reloading.
                string lastAreaSID = patch_AreaData.Get(saveData?.LastArea.ID ?? -1)?.ToKey().GetSID() ?? AreaKey.Default.GetSID();

                // Reload AreaData
                AreaData.Unload();
                AreaData.Load();
                AreaData.ReloadMountainViews();

                // Fake a save data reload to resync the save data to the new area list.
                if (saveData != null) {
                    saveData.LastArea = patch_AreaData.Get(lastAreaSID)?.ToKey() ?? AreaKey.Default;
                    saveData.BeforeSave();
                    saveData.AfterInitialize();
                }

                // Reload mountain data
                MTNExt.ReloadMod();
                MainThreadHelper.Schedule(() => MTNExt.ReloadModData());
            }

            // If we are on the overworld, fix it up
            if (Engine.Scene is Overworld overworld) {
                // If the camera was focused on a removed area, make it refocus a known area
                if (overworld.Mountain.Area >= AreaData.Areas.Count)
                    overworld.Mountain.EaseCamera(0, AreaData.Areas[0].MountainIdle, null, true);

                // Reload the chapter select
                OuiChapterSelect chapterSel = overworld.GetUI<OuiChapterSelect>();
                overworld.UIs.Remove(chapterSel);
                overworld.Remove(chapterSel);

                chapterSel = new OuiChapterSelect() { Visible = true };
                overworld.Add(chapterSel);
                overworld.UIs.Add(chapterSel);
                chapterSel.IsStart(overworld, (Overworld.StartMode) (-1));
            }
        }

        private static readonly FieldInfo f_Engine_scene = typeof(Engine).GetField("scene", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo f_Engine_nextScene = typeof(Engine).GetField("nextScene", BindingFlags.NonPublic | BindingFlags.Instance);
        private static void DoImmediateSceneSwitch(Scene newScene) {
            f_Engine_scene.SetValue(Engine.Instance, newScene);
            f_Engine_nextScene.SetValue(Engine.Instance, newScene);
        }

        /// <remarks>
        /// NOTE: Only safe to access from the main thread!
        /// </remarks>
        public static bool IsReloading => Engine.Scene is AssetReloadHelper;

        /// <remarks>
        /// NOTE: Only safe to access from the main thread!
        /// </remarks>
        public static Scene ReturnToScene { get; set; } // This previously used to be a property, so we have to preserve that for API compat

        /// <remarks>
        /// NOTE: Only safe to access from the main thread!
        /// </remarks>
        public static Action ReturnToGameLoop;

        private static async Task<AssetReloadHelper> StartReloadScene() {
            // Get the global reload semaphore
            await ReloadSemaphore.WaitAsync();

            try {
                // Capture the current scene
                ReturnToScene = Engine.Scene;
                ReturnToGameLoop = Engine.OverloadGameLoop;

                // Create a new asset reload helper scene, and make it the current one
                AssetReloadHelper helper = new AssetReloadHelper(ReturnToScene);
                DoImmediateSceneSwitch(helper);
                Engine.OverloadGameLoop = helper.Update;

                Logger.Log(LogLevel.Verbose, "reload", "Started non-silent reload; switched to reload helper scene");
                return helper;
            } catch {
                ReloadSemaphore.Release();
                throw;
            }
        }

        private static void EndReloadScene(AssetReloadHelper helper) {
            try {
                // Return back to the scene
                Engine.OverloadGameLoop = ReturnToGameLoop;

                DoImmediateSceneSwitch(helper.OrigScene);
                if (helper.OrigScene != ReturnToScene) {
                    Engine.Scene = ReturnToScene; // Set nextScene to simulate a transition
                    helper.OrigScene?.End();
                    DoImmediateSceneSwitch(ReturnToScene);
                    ReturnToScene?.Begin();
                }

                ReturnToScene = null;
                ReturnToGameLoop = null;

                Logger.Log(LogLevel.Verbose, "reload", $"Finished non-silent reload; switched back to scene {Engine.Scene}");
            } finally {
                // Dispose the helper and release the reload semaphore
                helper.Dispose();
                ReloadSemaphore.Release();
            }
        }

        public readonly Scene OrigScene;

        private readonly ConcurrentQueue<(string text, Func<Task> task)> taskQueue = new ConcurrentQueue<(string, Func<Task>)>();
        private Task curTask;
        private string curTaskText, curDisplayText;

        private readonly WorkerThreadTaskScheduler workerTaskScheduler;
        private readonly TaskFactory workerTaskFactory;

        private const float TimeIn = 0.3f, TimeOut = 0.15f;
        private bool outTransition;
        private float transitionTimer;

        private bool cogwheelInit;
        private MTexture cogwheelTex;
        private float cogwheelRot;

        private bool capturedSnapshots;
        private Texture2D snapshotTex, snapshotGrayScaleTex;

        public CancellationToken CancellationToken => workerTaskScheduler.CancellationToken;

        private AssetReloadHelper(Scene origScene) {
            OrigScene = origScene;

            // Start the reload worker
            workerTaskScheduler = new WorkerThreadTaskScheduler("Everest Reload Worker");
            workerTaskFactory = new TaskFactory(workerTaskScheduler.CancellationToken, TaskCreationOptions.None, TaskContinuationOptions.None, workerTaskScheduler); 
        }

        public void Dispose() {
            workerTaskScheduler?.Dispose();

            snapshotTex?.Dispose();
            snapshotGrayScaleTex?.Dispose();
        }

        public override void End() {
            // End will never be called by us, as we bypass regular scene transitions
            // If its still called this means that an external source initiated a scene transition
            // As such cancel the reload (this is a bad idea; the initiator should had a *real* good reason for doing this)
            Logger.Log(LogLevel.Warn, "reload", "Cancelling reload as an external source initiated a scene transition - THIS IS A BAD IDEA, THINGS MIGHT BREAK");

            if (Engine.OverloadGameLoop == Update)
                Engine.OverloadGameLoop = ReturnToGameLoop;

            ReturnToScene = null;
            ReturnToGameLoop = null;

            base.End();
            Dispose();

            ReloadSemaphore.Release();
        }

        public override void Update() {
            base.Update();

            // Update the cogwheel rotation
            cogwheelRot += 4 * Engine.RawDeltaTime;

            // Update the in-out lerp timer
            transitionTimer += Engine.RawDeltaTime;
            if (transitionTimer > (outTransition ? TimeOut : TimeIn))
                transitionTimer = (outTransition ? TimeOut : TimeIn);

            // Update the current task
            while (curTask?.IsCompleted ?? true) {
                // Check if a reload task failed
                if ((curTask?.IsFaulted ?? false) && curTask?.Exception is AggregateException ex) {
                    Logger.Log(LogLevel.Error, "reload", $"Error while executing reload task '{curTaskText}':");
                    Logger.LogDetailed(ex, "reload");

                    // Trigger a crash screen
                    CriticalErrorHandler.HandleCriticalError(ExceptionDispatchInfo.Capture(ex))?.Throw();
                    return;
                }

                // Try to dequeue a new task
                if (!taskQueue.TryDequeue(out var newTask)) {
                    // There are no new tasks
                    if (curTask != null)
                        Logger.Log(LogLevel.Verbose, "reload", $"Finished reload task '{curTaskText}'");

                    curTask = null;
                    break;
                }

                // Switch to the new task
                if (curTask != null)
                    Logger.Log(LogLevel.Verbose, "reload", $"Finished reload task '{curTaskText}'; moving onto task '{newTask.text}'");
                else
                    Logger.Log(LogLevel.Verbose, "reload", $"Starting reload task '{newTask.text}'");

                curTask = newTask.task();
                curTaskText = newTask.text;
            }

            if (curTaskText != null)
                curDisplayText = curTaskText;

            // Check if all pending reload tasks have been completed, and start the out transition
            // Also handle new tasks which are added to the queue during the transition
            if (!outTransition && transitionTimer >= TimeIn && curTask == null && !workerTaskScheduler.HasWork) {
                outTransition = true;
                transitionTimer = 0;
            } else if (outTransition && (curTask != null || workerTaskScheduler.HasWork)) {
                Logger.Log(LogLevel.Info, "reload", "A new reload task was enqueued during the out transition, restarting...");
                outTransition = false;
                transitionTimer = TimeIn * (1 - transitionTimer / TimeOut);
            }

            // Check if the out transition is complete
            if (outTransition && transitionTimer >= TimeOut) {
                // Remove the current helper reference
                lock (_CurrentHelperSceneLock) {
                    if (!taskQueue.IsEmpty || workerTaskScheduler.HasWork)
                        // A new task was enqueued before we managed to clear the current helper
                        goto abortCompletion;

                    CurrentHelperScene = null;
                }

                // Reset the game's state
                EndReloadScene(this);
                return;
            }
            abortCompletion:;
        }

        public override void BeforeRender() {
            // Capture snapshots of the old scene
            if (!capturedSnapshots) {
                CaptureSnapshots(ReturnToScene);
                capturedSnapshots = true;
            }

            base.BeforeRender();

            // Render to the hires buffer if we have one
            if (HiresRenderer.Buffer == null)
                return;

            Engine.Graphics.GraphicsDevice.SetRenderTarget(HiresRenderer.Buffer);
            Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
            RenderContent(true);
        }

        public override void Render() {
            base.Render();

            Engine.Graphics.GraphicsDevice.Clear(Color.Black);

            // Render the hires buffer if we rendered to it, otherwise directly render to the backbuffer
            if (HiresRenderer.Buffer == null) {
                RenderContent(false);
                return;
            }

            Draw.SpriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.Default,
                RasterizerState.CullNone,
                null,
                Engine.ScreenMatrix
            );
            Draw.SpriteBatch.Draw(HiresRenderer.Buffer, new Vector2(-1f, -1f), Color.White);
            Draw.SpriteBatch.End();
        }

        private void CaptureSnapshots(Scene scene) {
            GraphicsDevice graphicsDev = Engine.Instance.GraphicsDevice;

            // Render the scene and get the back buffer data
            int width = graphicsDev.PresentationParameters.BackBufferWidth, height = graphicsDev.PresentationParameters.BackBufferHeight;
            Color[] backbufData = new Color[width * height];

            try {
                scene.BeforeRender();
        
                graphicsDev.Viewport = Engine.Viewport;
                graphicsDev.SetRenderTarget(null);
                graphicsDev.Clear(Engine.ClearColor);
                scene.Render();

                scene.AfterRender();
                graphicsDev.GetBackBufferData<Color>(backbufData);
            } catch (Exception ex) {
                Logger.Log(LogLevel.Warn, "reload", $"Failed to render original scene for reload snapshot:");
                Logger.LogDetailed(ex, "reload");
            } finally {
                graphicsDev.SetRenderTarget(null);

                try {
                    Draw.SpriteBatch.End();
                } catch {}
            }

            // Create the snapshot texture
            snapshotTex?.Dispose();
            snapshotTex = new Texture2D(graphicsDev, width, height, false, SurfaceFormat.Color);
            snapshotTex.SetData(backbufData);

            // Convert the color data to grayscale and create the grayscale snapshot texture
            for (int i = 0; i < backbufData.Length; i++) {
                Color col = backbufData[i];
                int gsVal = Calc.Clamp((int) ((col.R * 0.3f) + (col.G * 0.59f) + (col.B * 0.11f)), 0, 255);
                backbufData[i] = new Color(gsVal, gsVal, gsVal, col.A);
            }

            snapshotGrayScaleTex?.Dispose();
            snapshotGrayScaleTex = new Texture2D(graphicsDev, width, height, false, SurfaceFormat.Color);
            snapshotGrayScaleTex.SetData(backbufData);
        }

        private static readonly Vector2 CogwheelAnchor = new Vector2(96f, 96f);
        private void RenderContent(bool toHiresBuffer) {
            Draw.SpriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                toHiresBuffer ? Matrix.Identity : Engine.ScreenMatrix
            );

            try {
                // Calculate lerp variables
                float lerp = outTransition ? 1 - (transitionTimer / TimeOut) : (transitionTimer / TimeIn);
                float alphaLerp = Ease.SineInOut(lerp);

                // Draw the snapshot while desaturating it
                float border = MathHelper.Lerp(0f, 0.01f, outTransition ? Ease.CubeOut(lerp) : (1f - Ease.ElasticIn(1f - lerp)));
                Rectangle dest = new Rectangle(
                    (int) (Celeste.TargetWidth * border),
                    (int) (Celeste.TargetHeight * border),
                    (int) (Celeste.TargetWidth * (1 - 2*border)),
                    (int) (Celeste.TargetHeight * (1 - 2*border))
                );

                Draw.SpriteBatch.Draw(snapshotTex, dest, Color.White * MathHelper.Lerp(1f, 0.25f, alphaLerp));
                Draw.SpriteBatch.Draw(snapshotGrayScaleTex, dest, Color.White * MathHelper.Lerp(0f, 0.45f, alphaLerp));

                // Draw the cogwheel texture
                if (!cogwheelInit) {
                    cogwheelTex = GFX.Gui != null ? GFX.Gui["reloader/cogwheel"] : null;
                    cogwheelInit = true;
                }

                if (cogwheelInit && !(cogwheelTex?.Texture?.Texture?.IsDisposed ?? true)) {
                    Vector2 pos = CogwheelAnchor;
                    float scale = MathHelper.Lerp(0.2f, 0.25f, Ease.CubeOut(alphaLerp));

                    for (int x = -2; x <= 2; x++)
                        for (int y = -2; y <= 2; y++)
                            if (x != 0 || y != 0)
                                cogwheelTex.DrawCentered(CogwheelAnchor + new Vector2(x, y), Color.Black * Ease.CubeIn(alphaLerp), scale, cogwheelRot);
                    cogwheelTex.DrawCentered(CogwheelAnchor, Color.White * alphaLerp, scale, cogwheelRot);
                }

                // Draw the status text
                try {
                    Vector2 pos = CogwheelAnchor + Vector2.UnitX * 48f;
                    if (curDisplayText != null && Dialog.Language != null && ActiveFont.Font != null) {
                        Vector2 size = ActiveFont.Measure(curDisplayText);
                        ActiveFont.DrawOutline(curDisplayText, pos + Vector2.UnitX * size.X / 2, Vector2.One / 2, Vector2.One * MathHelper.Lerp(0.8f, 1f, Ease.CubeOut(alphaLerp)), Color.White * alphaLerp, 2, Color.Black * Ease.CubeIn(alphaLerp));
                    }
                } catch {
                    // Whoops, we weren't ready to draw text yet...
                }
            } catch (Exception ex) {
                Logger.Log(LogLevel.Warn, "reload", $"Failed to draw reload scene contents:");
                Logger.LogDetailed(ex, "reload");
            } finally {
                Draw.SpriteBatch.End();
            }
        }

    }
}
