using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;

namespace Celeste.Mod.Entities {
    public class CustomHeightDisplay : Entity {

        private bool drawText => ease > 0f && !string.IsNullOrEmpty(Text);

        public string Text { get; }
        private string leftText;
        private string rightText;

        private float leftSize;
        private float numberSize;

        private Vector2 size;

        public int Target { get; }
        public float Approach { get; private set; }
        private bool hasCount;

        private float ease;
        private float pulse;

        private bool displayOnTransition;
        private string spawnedLevel;

        private bool audioProgressed; // Whether audio has already been progressed (or should not be progressed at all)
        private bool easingCamera;

        private string displaySound;

        /// <summary></summary>
        /// <param name="dialog">Dialog ID, replaces first occurence of "{X}" with the counter.</param>
        /// <param name="target">Target for the counter.</param>
        /// <param name="from">Counter start.</param>
        /// <param name="progressAudio">Whether to increment audio progression.</param>
        /// <param name="displayOnTransition"></param>
        public CustomHeightDisplay(string dialog, int target, int from = 0, bool progressAudio = false, bool displayOnTransition = false)
            : base() {
            Text = dialog;
            leftText = "";
            rightText = "";

            this.displayOnTransition = displayOnTransition;
            easingCamera = true;

            // Just pretend audio was already progressed if not needed
            audioProgressed = !progressAudio;

            if (target > from) {
                if (Dialog.Has(dialog))
                    Text = Dialog.Get(dialog).ToUpper();

                Target = target;
                Approach = from;

                int idx = Text.IndexOf("{x}", StringComparison.InvariantCultureIgnoreCase);
                if (idx != -1) {
                    hasCount = true;
                    displaySound = SFX.game_07_altitudecount;

                    leftText = Text.Substring(0, idx);
                    if (idx + 3 < Text.Length)
                        rightText = Text.Substring(idx + 3);

                    leftSize = ActiveFont.Measure(leftText).X;
                    numberSize = ActiveFont.Measure(Target.ToString()).X;

                    size = ActiveFont.Measure(leftText + Target + rightText);
                } else {
                    displaySound = SFX.game_07_checkpointconfetti;

                    leftText = Text;
                    size = ActiveFont.Measure(leftText);
                }
            }

            Tag = Tags.HUD | Tags.Persistent | Tags.TransitionUpdate;
            Add(new Coroutine(Routine()));
        }

        public override void Added(Scene scene) {
            base.Added(scene);
            spawnedLevel = SceneAs<Level>().Session.Level;
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

            if (!string.IsNullOrEmpty(Text))
                Audio.Play(displaySound);
            while ((ease += Engine.DeltaTime / 0.15f) < 1f)
                yield return null;

            float displayTimer = 0f;
            while (Approach < Target && player.Scene != null && (!player.OnGround(1) || displayTimer < 0.5f) && !(displayTimer > 3f)) {
                displayTimer += Engine.DeltaTime;
                yield return null;
            }

            Approach = Target;
            if (hasCount) {
                pulse = 1f;
                while ((pulse -= Engine.DeltaTime * 4f) > 0f)
                    yield return null;
                pulse = 0f;
            }

            yield return 1f;
            while ((ease -= Engine.DeltaTime / 0.15f) > 0f)
                yield return null;

            RemoveSelf();
        }

        private IEnumerator CameraUp() {
            easingCamera = true;
            Level level = SceneAs<Level>();
            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 1.5f) {
                level.Camera.Y = level.Bounds.Bottom - 180 + 64f * (1f - Ease.CubeOut(p));
                yield return null;
            }
        }

        private void StepAudioProgression() {
            if (!audioProgressed) {
                Session session = SceneAs<Level>().Session;
                audioProgressed = true; // Make sure audio only ever progressed once
                session.Audio.Music.Progress++;
                session.Audio.Apply(false);
            }
        }

        public override void Update() {
            if (ease > 0f && hasCount) {
                // This could be made to scale better for larger/smaller values
                if (Target - Approach > 100f) {
                    Approach += 1000f * Engine.DeltaTime;
                } else if (Target - Approach > 25f) {
                    Approach += 200f * Engine.DeltaTime;
                } else if (Target - Approach > 5f) {
                    Approach += 50f * Engine.DeltaTime;
                } else if (Target - Approach > 0f) {
                    Approach += 10f * Engine.DeltaTime;
                } else {
                    Approach = Target;
                }
            }

            if (!easingCamera) {
                Level level = SceneAs<Level>();
                level.Camera.Y = level.Bounds.Bottom - 180 + 64;
            }
            base.Update();
        }

        public override void Render() {
            if (Scene.Paused)
                return;

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
                    ActiveFont.Draw(((int) Approach).ToString(), textPosition + Vector2.UnitX * (leftSize + numberSize * 0.5f) * scaleFactor, new Vector2(0.5f, 0.5f), scale, textColor);
            }
        }

        public override void Removed(Scene scene) {
            StepAudioProgression();
            base.Removed(scene);
        }

    }
}