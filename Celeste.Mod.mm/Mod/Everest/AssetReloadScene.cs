using FMOD.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public class AssetReloadScene : Scene {

        private static readonly FieldInfo f_Engine_scene = typeof(Engine).GetField("scene", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Queue<ReloadAction> QueuePending = new Queue<ReloadAction>();
        private static readonly Queue<ReloadAction> QueueDone = new Queue<ReloadAction>();
        private static ReloadAction Current;
        private static Thread Worker;

        private bool init;
        private bool done;

        public static Scene ReturnToScene;
        public static Action ReturnToGameLoop;

        private Texture2D snap;
        private Texture2D snapDesat;

        private MTexture cogwheel;

        private float time;
        private float timeTotal;
        private const float timeIn = 0.4f;
        private const float timeOut = 0.2f;

        public AssetReloadScene() {
        }

        public static void Do(string text, Action reload = null, Action done = null) {
            lock (QueuePending) {
                lock (QueueDone) {
                    ReloadAction action = new ReloadAction {
                        Text = text,
                        Reload = reload,
                        Done = done
                    };
                    (reload != null ? QueuePending : QueueDone).Enqueue(action);

                    if (Current == null)
                        Current = action;

                    Scene scene = Engine.Scene;
                    if (scene is AssetReloadScene reloadScene) {
                        if (!reloadScene.done)
                            return;
                        reloadScene.End();
                    } else {
                        ReturnToScene = scene;
                        ReturnToGameLoop = Engine.OverloadGameLoop;
                    }

                    reloadScene = new AssetReloadScene();
                    f_Engine_scene.SetValue(Engine.Instance, reloadScene);
                    Engine.OverloadGameLoop = () => {
                        reloadScene.Update();
                    };
                }
            }
        }

        public override void Begin() {
            base.Begin();

            if (GFX.Gui != null) {
                cogwheel = GFX.Gui["reloader/cogwheel"];
            }

            GraphicsDevice gd = Celeste.Instance.GraphicsDevice;
            PresentationParameters pp = gd.PresentationParameters;
            int w = gd.Viewport.Width;
            int h = gd.Viewport.Height;
            Color[] data = new Color[w * h];
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
                    lock (QueuePending) {
                        ReloadAction action = QueuePending.Dequeue();
                        Current = action;
                        action.Reload?.Invoke();
                        lock (QueueDone) {
                            QueueDone.Enqueue(action);
                        }
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
            }

            base.Update();

            time += Engine.RawDeltaTime;
            timeTotal += Engine.RawDeltaTime;

            if (done) {
                if (time >= timeOut) {
                    End();
                    Engine.OverloadGameLoop = ReturnToGameLoop;
                    f_Engine_scene.SetValue(Engine.Instance, ReturnToScene);
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
            float border = MathHelper.Lerp(0f, 0.05f, st);
            float a = Ease.SineInOut(t);

            Rectangle dest = new Rectangle(
                (int) (w * border),
                (int) (h * border),
                (int) (w * (1f - border * 2f)),
                (int) (h * (1f - border * 2f))
            );

            Draw.SpriteBatch.Draw(snap, dest, Color.White * MathHelper.Lerp(1f, 0.4f, a));
            Draw.SpriteBatch.Draw(snapDesat, dest, Color.White * MathHelper.Lerp(0f, 0.6f, a));

            Vector2 center = new Vector2(w * 0.5f, h * 0.5f);

            Vector2 pos = center + new Vector2(0, -32f);
            float cogScale = MathHelper.Lerp(0.5f, 0.7f, Ease.CubeOut(a));
            if (cogwheel != null) {
                float cogRot = timeTotal * 4f;
                for (int x = -2; x <= 2; x++)
                    for (int y = -2; y <= 2; y++)
                        if (x != 0 || y != 0)
                            cogwheel.DrawCentered(pos + new Vector2(x, y), Color.Black * a * a * a * a, cogScale, cogRot);
                cogwheel.DrawCentered(pos, Color.White * a, cogScale, cogRot);
            }

            pos = center + new Vector2(0, 96f);
            ReloadAction action = Current;
            try {
                if (action?.Text != null && Dialog.Language != null && ActiveFont.Font != null) {
                    ActiveFont.DrawOutline(action.Text, pos, new Vector2(0.5f, 0.5f), Vector2.One * MathHelper.Lerp(0.8f, 1f, Ease.CubeOut(a)), Color.White * a, 2f, Color.Black * a * a * a * a);
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
