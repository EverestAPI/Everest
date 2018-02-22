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
    // Based on MemorialText
    public class CustomMemorialText : Entity {

        public bool Show;
        public bool Dreamy;

        public CustomMemorial Memorial;

        private float index;
        private string message;
        private float alpha = 0f;
        private float timer = 0f;
        private float widestCharacter = 0f;
        private int firstLineLength;
        private SoundSource textSfx;
        private bool textSfxPlaying;

        public CustomMemorialText(CustomMemorial memorial, bool dreamy, string text)
            : base() {
            AddTag(Tags.HUD);
            AddTag(Tags.PauseUpdate);
            Add(textSfx = new SoundSource());

            Dreamy = dreamy;
            Memorial = memorial;
            message = text;

            firstLineLength = CountToNewline(0);

            for (int i = 0; i < message.Length; i++) {
                float width = ActiveFont.Measure(message[i]).X;
                if (width > widestCharacter) {
                    widestCharacter = width;
                }
            }
            widestCharacter *= 0.9f;
        }

        public override void Update() {
            base.Update();

            if (((Level) Scene).Paused) {
                textSfx.Pause();
                return;
            }

            timer += Engine.DeltaTime;

            if (!Show) {
                alpha = Calc.Approach(alpha, 0f, Engine.DeltaTime);
                if (alpha <= 0f) {
                    index = firstLineLength;
                }

            } else {
                alpha = Calc.Approach(alpha, 1f, Engine.DeltaTime * 2f);
                if (alpha >= 1f) {
                    index = Calc.Approach(index, message.Length, 32f * Engine.DeltaTime);
                }
            }

            if (Show && alpha >= 1f && index < message.Length) {
                if (!textSfxPlaying) {
                    textSfxPlaying = true;
                    textSfx.Play(Dreamy ? "event:/ui/game/memorial_dream_text_loop" : "event:/ui/game/memorial_text_loop", null, 0f);
                    textSfx.Param("end", 0f);
                }

            } else if (textSfxPlaying) {
                textSfxPlaying = false;
                textSfx.Stop(true);
                textSfx.Param("end", 1f);
            }

            textSfx.Resume();
        }

        private int CountToNewline(int start) {
            int i;
            for (i = start; i < message.Length; i++) {
                if (message[i] == '\n') {
                    break;
                }
            }
            return i - start;
        }

        public override void Render() {
            Level level = (Level) Scene;
            if (level.FrozenOrPaused || level.Completed) {
                return;
            }

            if (index <= 0f || alpha <= 0f) {
                return;
            }

            Camera camera = level.Camera;
            Vector2 pos = new Vector2((Memorial.X - camera.X) * 6f, (Memorial.Y - camera.Y) * 6f - 350f - ActiveFont.LineHeight * 3.3f);
            float alphaEased = Ease.CubeInOut(alpha);
            int length = (int) Math.Min(message.Length, index);
            int lineIndex = 0;
            float sink = 64f * (1f - alphaEased);
            int lineLength = CountToNewline(0);
            for (int i = 0; i < length; i++) {
                char c = message[i];
                if (c == '\n') {
                    lineIndex = 0;
                    lineLength = CountToNewline(i + 1);
                    sink += ActiveFont.LineHeight * 1.1f;
                    continue;
                }

                float xJustify = 1f;
                float x = -lineLength * widestCharacter / 2f + (lineIndex + 0.5f) * widestCharacter;
                float yOffs = 0f;

                if (Dreamy && c != ' ' && c != '-' && c != '\n') {
                    c = message[(i + (int) (Math.Sin((timer * 2f + i / 8f)) * 4.0) + message.Length) % message.Length];
                    yOffs = (float) Math.Sin((timer * 2f + i / 8f)) * 8f;
                    xJustify = (Math.Sin((timer * 4f + i / 16f)) < 0.0) ? -1f : 1f;
                }

                ActiveFont.Draw(c, pos + new Vector2(x, sink + yOffs), new Vector2(0.5f, 1f), new Vector2(xJustify, 1f), Color.White * alphaEased);
                lineIndex++;
            }
        }

    }
}
