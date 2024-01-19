using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    public class OuiLoggedProgress : Oui {

        public string Title;

        public Task Task;

        public List<string> Lines;

        public int Progress;
        public int ProgressMax;

        public bool WaitForConfirmOnFinish;
        public event Action OnFinish;
        private Action gotoAction;

        private float alpha = 0f;
        private float time = 0f;

        private string audioPrevMusic;
        private string audioPrevAmbience;

        private Rectangle logBounds = new Rectangle(0, 0, 1792, 768);

        private VirtualRenderTarget logBuffer;

        public OuiLoggedProgress() {
        }

        public OuiLoggedProgress Init<T>(string title, Action task, int max) where T : Oui
            => Init<T>(title, new Task(task), max);
        public OuiLoggedProgress Init<T>(string title, Task task, int max) where T : Oui {
            Title = title;
            Task = task;
            Lines = new List<string>();
            Progress = 0;
            ProgressMax = max;

            OnFinish += (gotoAction = () => Overworld.Goto<T>());

            if (task.Status == TaskStatus.Created)
                task.Start();

            return this;
        }

        public OuiLoggedProgress SwitchGoto<T>() where T : Oui {
            if (gotoAction != null)
                OnFinish -= gotoAction;
            OnFinish += (gotoAction = () => Overworld.Goto<T>());
            return this;
        }

        public void LogLine(string line, bool logToLogger = true) {
            if (logToLogger)
                Logger.Verbose("progress", line);

            int indexOfNewline;
            while ((indexOfNewline = line.IndexOf('\n')) != -1) {
                LogLine(line.Substring(0, indexOfNewline), false);
                line = line.Substring(indexOfNewline + 1);
            }

            StringBuilder escaped = new StringBuilder();
            for (int i = 0; i < line.Length; i++) {
                char c = line[i];
                if (!ActiveFont.Font.Get(ActiveFont.BaseSize * 0.5f).Characters.ContainsKey(c))
                    c = ' ';
                escaped.Append(c);
            }

            Lines.Add(escaped.ToString());
        }

        public override IEnumerator Enter(Oui from) {
            Overworld.ShowInputUI = false;

            audioPrevMusic = Audio.GetEventName(Audio.CurrentMusicEventInstance);
            Audio.SetMusic(null);
            audioPrevAmbience = Audio.GetEventName(Audio.CurrentAmbienceEventInstance);
            Audio.SetAmbience(null);

            Visible = true;

            for (float t = 0f; t < 1f; t += Engine.DeltaTime * 4f) {
                alpha = Ease.CubeOut(t);
                yield return null;
            }
        }

        public override IEnumerator Leave(Oui next) {
            Overworld.ShowInputUI = true;

            Audio.SetMusic(audioPrevMusic);
            Audio.SetAmbience(audioPrevAmbience);
            Audio.Play(SFX.ui_main_whoosh_large_out);

            for (float t = 0f; t < 1f; t += Engine.DeltaTime * 4f) {
                alpha = 1f - Ease.CubeIn(t);
                yield return null;
            }

            Visible = false;
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            Add(new BeforeRenderHook(BeforeRender));
        }

        public override void Removed(Scene scene) {
            logBuffer?.Dispose();
            logBuffer = null;

            base.Removed(scene);
        }

        public override void Update() {
            if (Task != null && (Task.IsCompleted || Task.IsCanceled || Task.IsFaulted)) {
                if (Task.IsFaulted) {
                    LogLine(">>>>> FATAL ERROR: WORKER TASK HAS FAULTED - THIS IS A BUG <<<<<");
                    WaitForConfirmOnFinish = true;
                }

                if (!WaitForConfirmOnFinish || Input.MenuConfirm.Pressed) {
                    OnFinish?.Invoke();
                    Task = null;
                }
            }

            time += Engine.DeltaTime;

            base.Update();
        }

        public void BeforeRender() {
            if (!Focused || !Visible || Lines == null)
                return;

            if (logBuffer == null)
                logBuffer = VirtualContent.CreateRenderTarget("loggedprogress-log", logBounds.Width, logBounds.Height);
            Engine.Graphics.GraphicsDevice.SetRenderTarget(logBuffer.Target);
            Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

            Draw.SpriteBatch.Begin();

            for (int i = 0; i < Lines.Count; i++) {
                ActiveFont.Draw(
                    Lines[i],
                    new Vector2(8f, logBuffer.Height - 8f - (30f * (Lines.Count - i))),
                    Vector2.Zero,
                    Vector2.One * 0.5f,
                    Color.White
                );
            }

            Draw.SpriteBatch.End();
        }

        public override void Render() {
            if (alpha > 0f)
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * alpha * 0.6f);
            base.Render();

            if (Lines == null)
                return;

            ActiveFont.DrawEdgeOutline(Title, new Vector2(960f, 128f), new Vector2(0.5f, 0.5f), Vector2.One * 2f, Color.Gray * alpha, 4f, Color.DarkSlateBlue * alpha, 2f, Color.Black * (alpha * alpha * alpha));

            Draw.Rect(64f, 128f + 64f + 2f, 1920f - 2f * 64f, 4f, Color.Black * alpha * 0.8f);
            if (ProgressMax > 0) {
                Draw.Rect(64f, 128f + 64f, (Progress / (float) ProgressMax) * (1920f - 2f * 64f), 8f, Color.White * alpha * 0.8f);
            } else {
                float t = (time * 4f) % 2f;
                if (t < 1f) {
                    Draw.Rect(64f, 128f + 64f, t * (1920f - 2f * 64f), 8f, Color.White * alpha * 0.8f);
                } else {
                    t -= 1f;
                    Draw.Rect(64f + t * (1920f - 2f * 64f), 128f + 64f, (1f - t) * (1920f - 2f * 64f), 8f, Color.White * alpha * 0.8f);
                }
            }

            Rectangle log = new Rectangle(1920 / 2 - logBounds.Width / 2, 128 + 64 + 16, logBounds.Width, logBounds.Height);
            Draw.Rect(log, Color.Black * alpha * 0.8f);
            if (logBuffer != null)
                Draw.SpriteBatch.Draw(logBuffer.Target, log, Color.White * alpha);

        }


    }
}
