using FMOD.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public class AssetReloadHelper : Scene {

        private static readonly FieldInfo f_Engine_scene = typeof(Engine).GetField("scene", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo f_Engine_nextScene = typeof(Engine).GetField("nextScene", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Queue<ReloadAction> QueuePending = new Queue<ReloadAction>();
        private static readonly Queue<ReloadAction> QueueDone = new Queue<ReloadAction>();
        private static ReloadAction Current;
        private static Thread Worker;

        private bool init;
        private bool initRender;
        private bool done;

        private static Scene _ReturnToSceneOrig;
        private static Scene _ReturnToScene;
        public static Scene ReturnToScene {
            get => _ReturnToScene;
            set {
                if (value == null || _ReturnToSceneOrig == null)
                    _ReturnToSceneOrig = value;
                _ReturnToScene = value;
            }
        }
        public static Action ReturnToGameLoop;

        public static readonly HashSet<string> SilentThreadList = new HashSet<string>() {
            "Main Thread",
            "GAME_LOADER",
            "OVERWORLD_LOADER",
            "SUMMIT_VIGNETTE",
            "LEVEL_LOADER",
            "COMPLETE_LEVEL",
            "Wave Dash Presentation Loading",
        };

        private static bool ReloadingLevel;
        private static bool ReloadingLevelPaused;

        private Texture2D snap;
        private Texture2D snapDesat;

        private MTexture cogwheel;

        private float time;
        private float timeTotal;
        private const float timeIn = 0.3f;
        private const float timeOut = 0.15f;

        public AssetReloadHelper() {
        }

        public static void Do(string text, Action reload = null, Action done = null)
            => Do(false, text, reload, done);
        public static void Do(bool silent, string text, Action reload = null, Action done = null) {
            if (Celeste.LoadTimer != null ||
                (silent && SilentThreadList.Contains(Thread.CurrentThread.Name))) {
                reload?.Invoke();
                done?.Invoke();
                return;
            }

            lock (QueuePending) {
                lock (QueueDone) {
                    Scene scene = Engine.Scene;
                    if (scene == null) {
                        // Wait until there is a scene.
                        MainThreadHelper.Do(() => Do(silent, text, reload, done));
                        return;
                    }

                    ReloadAction action = new ReloadAction {
                        Text = text,
                        Reload = reload,
                        Done = done
                    };
                    QueuePending.Enqueue(action);

                    if (Current == null)
                        Current = action;

                    if (scene is AssetReloadHelper reloadScene) {
                        if (!reloadScene.done)
                            return;
                        reloadScene.End();
                    } else {
                        ReturnToScene = scene;
                        ReturnToGameLoop = Engine.OverloadGameLoop;
                    }

                    reloadScene = new AssetReloadHelper();
                    f_Engine_scene.SetValue(Engine.Instance, reloadScene);
                    f_Engine_nextScene.SetValue(Engine.Instance, reloadScene);
                    Engine.OverloadGameLoop = () => {
                        reloadScene.Update();
                    };
                }
            }
        }

        public static void ReloadLevel() {
            lock (QueuePending) {
                if (Celeste.LoadTimer != null || ReloadingLevel)
                    return;

                Level level = Engine.Scene as Level ?? ReturnToScene as Level;
                if (level == null)
                    return;

                ReloadingLevel = true;
                Do($"Reloading level", () => {
                    LevelLoader loader = new LevelLoader(level.Session, level.Session.RespawnPoint);

                    Player player = level.Tracker?.GetEntity<Player>();
                    if (player != null) {
                        patch_Level.SkipScreenWipes++;

                        patch_Level.NextLoadedPlayer = player;

                        player.Remove(player.Light);
                        VertexLight light = player.Light;
                        player.Add(light = player.Light = new VertexLight(light.Position, light.Color, light.Alpha, (int) light.StartRadius, (int) light.EndRadius));

                        ((patch_Player) player).OverrideIntroType = Player.IntroTypes.Transition;
                    }

                    ReturnToScene = loader;
                    ReloadingLevel = false;
                    ReloadingLevelPaused = level.Paused;

                    while (!loader.Loaded)
                        Thread.Yield();
                });
            }
        }

        private void InitRender() {
            if (GFX.Gui != null) {
                cogwheel = GFX.Gui["reloader/cogwheel"];
            }

            GraphicsDevice gd = Celeste.Instance.GraphicsDevice;
            PresentationParameters pp = gd.PresentationParameters;
            int w = gd.Viewport.Width;
            int h = gd.Viewport.Height;
            Color[] data = new Color[w * h];

            // This shouldn't be done, yet here we are.

            Scene scene = _ReturnToScene;
            scene?.BeforeRender();
            gd.SetRenderTarget(null);
            gd.Viewport = Engine.Viewport;
            gd.Clear(Engine.ClearColor);
            scene?.Render();
            scene?.AfterRender();

            gd.SetRenderTarget(null);

            gd.GetBackBufferData(gd.Viewport.Bounds, data, 0, data.Length);

            snap = new Texture2D(gd, w, h, false, SurfaceFormat.Color);
            snap.SetData(data);

            for (int i = 0; i < data.Length; i++) {
                Color c = data[i];
                int g = Calc.Clamp((int) ((c.R * 0.3f) + (c.G * 0.59f) + (c.B * 0.11f)), 0, 255);
                data[i] = new Color(g, g, g, c.A);
            }
            snapDesat = new Texture2D(gd, w, h, false, SurfaceFormat.Color);
            snapDesat.SetData(data);
        }

        public override void Begin() {
            base.Begin();

            Worker = new Thread(WorkerLoop);
            Worker.Name = "Everest Reload Worker";
            Worker.IsBackground = true;
            Worker.Start();
        }

        public override void End() {
            base.End();

            snap?.Dispose();
            snapDesat?.Dispose();
        }

        private static void WorkerLoop() {
            try {
                while (QueuePending.Count > 0) {
                    ReloadAction action;
                    lock (QueuePending) {
                        action = QueuePending.Dequeue();
                    }
                    Current = action;
                    action.Reload?.Invoke();
                    lock (QueueDone) {
                        QueueDone.Enqueue(action);
                    }
                }

            } finally {
                Worker = null;
            }
        }

        public override void Update() {
            if (!init) {
                init = true;
                Begin();
                return;
            }

            base.Update();

            time += Engine.RawDeltaTime;
            timeTotal += Engine.RawDeltaTime;

            if (done) {
                if (time >= timeOut) {
                    End();
                    Engine.OverloadGameLoop = ReturnToGameLoop;

                    if (ReturnToScene == null)
                        ReturnToScene = new OverworldLoader(Overworld.StartMode.Titlescreen);

                    f_Engine_scene.SetValue(Engine.Instance, ReturnToScene);
                    f_Engine_nextScene.SetValue(Engine.Instance, ReturnToScene);

                    if (_ReturnToScene != _ReturnToSceneOrig) {
                        _ReturnToSceneOrig?.End();
                        _ReturnToScene?.Begin();
                    }

                    if (ReloadingLevelPaused && ReturnToScene is LevelLoader levelLoader)
                        levelLoader.Level.Pause();

                    ReturnToGameLoop = null;
                    ReturnToScene = null;
                    ReloadingLevelPaused = false;
                }
                return;
            }

            if (time < timeIn) {
                // Ease in.
            } else {
                time = timeIn;
            }

            if (QueueDone.Count > 0) {
                lock (QueueDone) {
                    foreach (ReloadAction action in QueueDone)
                        action.Done?.Invoke();
                    QueueDone.Clear();
                }
            }

            if (Worker == null) {
                time = 0f;
                done = true;
            }
        }

        public override void BeforeRender() {
            if (!init) {
                ReturnToScene.BeforeRender();
                return;
            }

            if (!initRender) {
                initRender = true;
                InitRender();
            }

            base.BeforeRender();

            if (HiresRenderer.Buffer == null)
                return;

            Engine.Graphics.GraphicsDevice.SetRenderTarget(HiresRenderer.Buffer);
            Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
            RenderContent(true);
        }

        public override void AfterRender() {
            if (!init) {
                ReturnToScene.AfterRender();
                return;
            }

            base.AfterRender();
        }

        public override void Render() {
            if (!init) {
                ReturnToScene.Render();
                return;
            }

            base.Render();

            Engine.Graphics.GraphicsDevice.Clear(Color.Black);

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

        private void RenderContent(bool toBuffer) {
            Draw.SpriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                toBuffer ? Matrix.Identity : Engine.ScreenMatrix
            );

            float w = 1920f;
            float h = 1080f;

            float t = done ? 1f - time / timeOut : (time / timeIn);

            float st = done ? Ease.CubeOut(t) : (1f - Ease.ElasticIn(1f - t));
            float border = MathHelper.Lerp(0f, 0.01f, st);
            float a = Ease.SineInOut(t);

            Rectangle dest = new Rectangle(
                (int) (w * border),
                (int) (h * border),
                (int) (w * (1f - border * 2f)),
                (int) (h * (1f - border * 2f))
            );

            Draw.SpriteBatch.Draw(snap, dest, Color.White * MathHelper.Lerp(1f, 0.25f, a));
            Draw.SpriteBatch.Draw(snapDesat, dest, Color.White * MathHelper.Lerp(0f, 0.45f, a));

            Vector2 anchor = new Vector2(96f, 96f);

            Vector2 pos = anchor + new Vector2(0f, 0f);
            float cogScale = MathHelper.Lerp(0.2f, 0.25f, Ease.CubeOut(a));
            if (!(cogwheel?.Texture?.Texture?.IsDisposed ?? true)) {
                float cogRot = timeTotal * 4f;
                for (int x = -2; x <= 2; x++)
                    for (int y = -2; y <= 2; y++)
                        if (x != 0 || y != 0)
                            cogwheel.DrawCentered(pos + new Vector2(x, y), Color.Black * a * a * a * a, cogScale, cogRot);
                cogwheel.DrawCentered(pos, Color.White * a, cogScale, cogRot);
            }

            pos = anchor + new Vector2(48f, 0f);
            ReloadAction action = Current;
            try {
                if (action?.Text != null && Dialog.Language != null && ActiveFont.Font != null) {
                    Vector2 size = ActiveFont.Measure(action.Text);
                    ActiveFont.DrawOutline(action.Text, pos + new Vector2(size.X * 0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.One * MathHelper.Lerp(0.8f, 1f, Ease.CubeOut(a)), Color.White * a, 2f, Color.Black * a * a * a * a);
                }
            } catch {
                // Whoops, we weren't ready to draw text yet...
            }

            Draw.SpriteBatch.End();
        }

        private class ReloadAction {
            public string Text;
            public Action Reload;
            public Action Done;
        }

    }
}
