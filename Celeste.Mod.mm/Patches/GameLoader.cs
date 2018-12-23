#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used
#pragma warning disable CS0414 // The field is assigned but its value is never used

using Celeste.Mod;
using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_GameLoader : GameLoader {

        // We're effectively in GameLoader, but still need to "expose" private fields to our mod.
        private Entity handler;
        private bool loaded;
        private bool audioLoaded;
        private bool audioStarted;
        private bool dialogLoaded;
        private bool ready;
        [MonoModIfFlag("Has:IntroSkip")]
        private bool skipped;

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
                SkipIntro();
            }
        }

        public extern void orig_Update();
        public override void Update() {
            if (!ready) {
                bool inputDisabled = MInput.Disabled;
                MInput.Disabled = false;

                if (Input.Pause.Pressed || Input.ESC.Pressed) {
                    if (Input.MenuDown.Check) {
                        Celeste.PlayMode = Celeste.PlayModes.Debug;
                        // Late-enable commands. This is normally set by Celeste.Initialize.
                        Engine.Commands.Enabled = true;
                    }

                    SkipIntro();

                }

                MInput.Disabled = inputDisabled;
            }

            // Note: You may instinctually call base.Update();
            // DON'T! The original method is orig_Update
            orig_Update();
        }

        [MonoModReplace]
        private void LoadThread() {
            Console.WriteLine("GAME DISPLAYED : " + Stopwatch.ElapsedMilliseconds + "ms");
            MInput.Disabled = true;

            Audio.Init();

            // Original code loads audio banks here.
            /*
            Audio.Banks.Master = Audio.Banks.Load("Master Bank", true);
            Audio.Banks.Music = Audio.Banks.Load("music", false);
            Audio.Banks.Sfxs = Audio.Banks.Load("sfx", false);
            Audio.Banks.UI = Audio.Banks.Load("ui", false);
            Audio.Banks.NewContent = Audio.Banks.Load("new_content", false);
            */

            Settings.Instance.ApplyVolumes();
            audioLoaded = true;

            Fonts.Load(); // Luckily, the textures for the fonts are preloaded.
            Dialog.Load();
            dialogLoaded = true;

            MInput.Disabled = false;

            if (!GFX.LoadedMainContent) {
                throw new Exception("GFX not loaded!");
            }
            GFX.LoadData(); // Load all related GFX metadata.

            AreaData.Load();

            if (!CoreModule.Settings.NonThreadedGL) {
                GFX.MountainTerrain = ObjModel.Create(Path.Combine(Engine.ContentDirectory, "Overworld", "mountain.obj"));
                GFX.MountainBuildings = ObjModel.Create(Path.Combine(Engine.ContentDirectory, "Overworld", "buildings.obj"));
                GFX.MountainCoreWall = ObjModel.Create(Path.Combine(Engine.ContentDirectory, "Overworld", "mountain_wall.obj"));
            }
            // Otherwise loaded in CoreModule.LoadContent

            Console.WriteLine("LOADED : " + Stopwatch.ElapsedMilliseconds + "ms");
            Stopwatch.Stop();
            Stopwatch = null;
            loaded = true;
        }

        [MonoModIfFlag("Has:IntroSkip")]
        private void SkipIntro() {
            skipped = true;
        }

        // If we're on a version < 1.1.9.2, relink all SkipIntro calls to SkipIntroOld.

        [MonoModIfFlag("Lacks:IntroSkip")]
        [MonoModLinkFrom("System.Void Celeste.GameLoader::SkipIntro()")]
        private void SkipIntroOld() {
            introRoutine.Cancel();
            introRoutine = null;
            handler.Add(new Coroutine(FastIntroRoutine()));
        }

        [MonoModIfFlag("Lacks:IntroSkip")]
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
        }

    }
}
