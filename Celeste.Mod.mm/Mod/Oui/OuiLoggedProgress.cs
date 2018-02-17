using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public class OuiLoggedProgress : Oui {

        public string Title;

        public Task Task;

        public List<string> Lines;

        public int Progress;
        public int ProgressMax;

        private Action exit;

        private float alpha = 0f;

        public OuiLoggedProgress() {
        }

        public OuiLoggedProgress Init<T>(string title, Task task, int max) where T : Oui {
            Title = title;
            Task = task;
            Lines = new List<string>();
            Progress = 0;
            ProgressMax = max;

            // exit = () => Overworld.Goto<T>();

            return this;
        }
        
        public void LogLine(string line) {
            Lines.Add(line);
        }

        public override IEnumerator Enter(Oui from) {
            Visible = true;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                alpha = Ease.CubeOut(p);
                yield return null;
            }
        }

        public override IEnumerator Leave(Oui next) {
            Audio.Play("event:/ui/main/whoosh_large_out");

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                alpha = 1f - Ease.CubeIn(p);
                yield return null;
            }

            Visible = false;
        }

        public override void Update() {
            if (Task != null && (Task.IsCompleted || Task.IsCanceled || Task.IsFaulted)) {
                // TODO: Press anything to exit OuiLoggedProgress?
                exit?.Invoke();
                Task = null;
            }

            base.Update();
        }

        public override void Render() {
            if (alpha > 0f)
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * alpha * 0.4f);
            base.Render();

            if (Lines == null)
                return;

            ActiveFont.DrawEdgeOutline(Title, new Vector2(960f, 128f), new Vector2(0.5f, 0.5f), Vector2.One * 2f, Color.Gray * alpha, 4f, Color.DarkSlateBlue * alpha, 2f, Color.Black * (alpha * alpha * alpha));

            float p = Progress / (float) ProgressMax;
            
            Draw.Rect(128f, 256f + 2f, 1920f - 256f, 4f, Color.Black * alpha * 0.4f);
            Draw.Rect(128f, 256f, p * (1920f - 256f), 8f, Color.White * alpha * 0.6f);

            Draw.Rect(128f, 256f + 128f, 1920f - 256f, 1080f - 256f - 128f - 128f, Color.Black * alpha * 0.8f);


        }


    }
}
