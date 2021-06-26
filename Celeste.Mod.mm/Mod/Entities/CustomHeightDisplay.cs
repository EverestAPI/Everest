using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;

namespace Celeste.Mod.Entities {
    public class CustomHeightDisplay : Entity {
        private bool drawText => ease > 0f && !string.IsNullOrEmpty(text);

        private string text;
        private string leftText;
        private string rightText;

        private float leftSize;
        private float numberSize;

        private Vector2 size;

        private int height;
        private float approach;
        private bool hasCount;

        private float ease;
        private float pulse;

        private bool displayOnTransition;
        private string spawnedLevel;

        private bool setAudioProgression;
        private bool easingCamera;

        private string displaySound;

        public CustomHeightDisplay(string dialog, int height, int from = 0, bool progressAudio = false, bool displayOnTransition = false) : base() {
            text = dialog;
            leftText = "";
            rightText = "";

            this.displayOnTransition = displayOnTransition;
            easingCamera = true;

            setAudioProgression = !progressAudio;

            if (height > from) {
                Console.WriteLine("Height greater than From");
                if (Dialog.Has(dialog))
                    text = Dialog.Get(dialog).ToUpper();

                this.height = height;
                approach = from;

                int idx = text.IndexOf("{X}");
                if (idx != -1) {
                    hasCount = true;
                    displaySound = SFX.game_07_altitudecount;

                    leftText = text.Substring(0, idx);
                    //if (idx + 3 < text.Length)
                        //rightText = text[(idx + 3)..];

                    leftSize = ActiveFont.Measure(leftText).X;
                    numberSize = ActiveFont.Measure(height.ToString()).X;

                    size = ActiveFont.Measure(leftText + height + rightText);
                } else {
                    displaySound = SFX.game_07_checkpointconfetti;

                    leftText = text;
                    size = ActiveFont.Measure(leftText);
                }
            }

            Tag = Tags.HUD | Tags.Persistent | Tags.TransitionUpdate;
            Add(new Coroutine(Routine()));
        }

        public override void Added(Scene scene) {
            base.Added(scene);
            spawnedLevel = (scene as Level).Session.Level;
        }

        private IEnumerator Routine() {
            Player player;
            while (((player = Scene.Tracker.GetEntity<Player>()) == null) || (displayOnTransition && SceneAs<Level>().Session.Level == spawnedLevel))
                yield return null;

            StepAudioProgression();
            easingCamera = !displayOnTransition;
            yield return 0.1f;

            if (displayOnTransition)
                Add(new Coroutine(CameraUp()));

            if (!string.IsNullOrEmpty(text))
                Audio.Play(displaySound);
            while ((ease += Engine.DeltaTime / 0.15f) < 1f)
                yield return null;

            float displayTimer = 0f;
            while (approach < height && player != null && (!player.OnGround(1) || displayTimer < 0.5f) && !(displayTimer > 3f)) {
                displayTimer += Engine.DeltaTime;
                yield return null;
            }

            approach = height;
            pulse = 1f;
            while ((pulse -= Engine.DeltaTime * 4f) > 0f)
                yield return null;

            pulse = 0f;
            yield return 1f;
            while ((ease -= Engine.DeltaTime / 0.15f) > 0f)
                yield return null;

            RemoveSelf();
            yield break;
        }

        private IEnumerator CameraUp() {
            easingCamera = true;
            Level level = Scene as Level;
            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 1.5f) {
                level.Camera.Y = level.Bounds.Bottom - 180 + 64f * (1f - Ease.CubeOut(p));
                yield return null;
            }
            yield break;
        }

        private void StepAudioProgression() {
            Session session = (Scene as Level).Session;
            if (!setAudioProgression) {
                setAudioProgression = true;
                session.Audio.Music.Progress++;
                session.Audio.Apply(false);
            }
        }

        public override void Update() {
            if (ease > 0f && hasCount) {
                if (height - approach > 100f) {
                    approach += 1000f * Engine.DeltaTime;
                } else if (height - approach > 25f) {
                    approach += 200f * Engine.DeltaTime;
                } else if (height - approach > 5f) {
                    approach += 50f * Engine.DeltaTime;
                } else if (height - approach > 0f) {
                    approach += 10f * Engine.DeltaTime;
                } else {
                    approach = height;
                }
            }

            if (!easingCamera) {
                Level level = Scene as Level;
                level.Camera.Y = level.Bounds.Bottom - 180 + 64;
            }
            base.Update();
        }

        public override void Render() {
            if (Scene.Paused) {
                return;
            }
            if (drawText) {
                Vector2 center = new Vector2(1920f, 1080f) / 2f;

                float scaleFactor = 1.2f + pulse * 0.2f;
                Vector2 scaledSize = size * scaleFactor;

                float ease = Ease.SineInOut(this.ease);

                Draw.Rect(center.X - (scaledSize.X + 64f) * 0.5f, center.Y - (scaledSize.Y + 32f) * 0.5f * ease, (scaledSize.X + 64f), (scaledSize.Y + 32f) * ease, Color.Black);

                Vector2 textPosition = center + new Vector2(-scaledSize.X * 0.5f, 0f);
                Vector2 scale = new Vector2(1f, ease) * scaleFactor;
                Color textColor = Color.White * ease;
                ActiveFont.Draw(leftText, textPosition, new Vector2(0f, 0.5f), scale, textColor);
                ActiveFont.Draw(rightText, textPosition + Vector2.UnitX * (leftSize + numberSize) * scaleFactor, new Vector2(0f, 0.5f), scale, textColor);
                if (hasCount)
                    ActiveFont.Draw(((int) approach).ToString(), textPosition + Vector2.UnitX * (leftSize + numberSize * 0.5f) * scaleFactor, new Vector2(0.5f, 0.5f), scale, textColor);
            }
        }

        public override void Removed(Scene scene) {
            StepAudioProgression();
            base.Removed(scene);
        }

    }
}
