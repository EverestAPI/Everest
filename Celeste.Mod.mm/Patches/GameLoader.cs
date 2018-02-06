#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_GameLoader : GameLoader {

        // We're effectively in GameLoader, but still need to "expose" private fields to our mod.
        private Entity handler;
        private bool loaded;

        private List<MTexture> loadingTextures;
        private float loadingFrame;
        private float loadingAlpha;

        // Add a new field as we need to access the intro coroutine later.
        private Coroutine introRoutine;

        public extern void orig_Begin();
        public override void Begin() {
            // Note: You may instinctually call base.Begin();
            // DON'T! The original method is orig_Begin
            orig_Begin();


            // Assume that the intro routine is the first added coroutine.
            foreach (Coroutine c in handler.Components) {
                introRoutine = c;
                break;
            }

            if (CoreModule.Settings.LaunchWithoutIntro && introRoutine != null) {
                introRoutine.Cancel();
                introRoutine = null;
                handler.Add(new Coroutine(FastIntroRoutine()));
            }
        }

        public IEnumerator FastIntroRoutine() {
            if (!loaded) {
                loadingTextures = GFX.Overworld.GetAtlasSubtextures("loading/");

                Image img = new Image(loadingTextures[0]);
                img.CenterOrigin();
                img.Scale = Vector2.One * 0.5f;
                handler.Add(img);

                while (!loaded || loadingAlpha > 0f) {
                    loadingFrame += Engine.DeltaTime * 10f;
                    loadingAlpha = Calc.Approach(loadingAlpha, loaded ? 0f : 1f, Engine.DeltaTime * 4f);

                    img.Texture = loadingTextures[(int) (loadingFrame % loadingTextures.Count)];
                    img.Color = Color.White * Ease.CubeOut(loadingAlpha);
                    img.Position = new Vector2(1792f, 1080f - 128f * Ease.CubeOut(loadingAlpha));
                    yield return null;
                }

                img = null;
            }

            Engine.Scene = new OverworldLoader(Overworld.StartMode.Titlescreen, Snow);
            yield break;
        }

        public extern void orig_Update();
        public override void Update() {
            if (introRoutine != null && (Input.Pause.Pressed || Input.ESC.Pressed)) {
                if (Input.MenuDown.Check) {
                    Celeste.PlayMode = Celeste.PlayModes.Debug;
                    // Late-enable commands. This is normally set by Celeste.Initialize.
                    Engine.Commands.Enabled = true;
                }
                introRoutine.Cancel();
                introRoutine = null;
                handler.Add(new Coroutine(FastIntroRoutine()));
            }

            // Note: You may instinctually call base.Update();
            // DON'T! The original method is orig_Update
            orig_Update();
        }

    }
}
