#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used
#pragma warning disable CS0414 // The field is assigned but its value is never used

using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.Helpers;
using Celeste.Mod.UI;
using Monocle;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Celeste {
    class patch_GameLoader : GameLoader {

        // We're effectively in GameLoader, but still need to "expose" private fields to our mod.
        private Entity handler;
        private bool loaded;
        private bool audioLoaded;
        private bool audioStarted;
        private bool dialogLoaded;
        private bool ready;
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
                skipped = true;
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

                    skipped = true;

                }

                MInput.Disabled = inputDisabled;
            }

            // Note: You may instinctually call base.Update();
            // DON'T! The original method is orig_Update
            orig_Update();
        }

        [MonoModReplace]
        private void LoadThread() {
            MInput.Disabled = true;
            Stopwatch timer = Stopwatch.StartNew();

            Audio.Init();
            // Original code loads audio banks here.
            Settings.Instance.ApplyVolumes();
            audioLoaded = true;
            Console.WriteLine(" - AUDIO LOAD: " + timer.ElapsedMilliseconds + "ms");
            timer.Stop();

            if (!CoreModule.Settings.NonThreadedGL) {
                GFX.Load();
                MTN.Load();
                GFX.LoadData();
                MTN.LoadData();
            }
            // Otherwise loaded in CoreModule.LoadContent

            timer = Stopwatch.StartNew();
            Fonts.Prepare();
            Dialog.Load();
            Fonts.Load(Dialog.Languages["english"].FontFace);
            Fonts.Load(Dialog.Languages[Settings.Instance.Language].FontFace);
            dialogLoaded = true;
            Console.WriteLine(" - DIA/FONT LOAD: " + timer.ElapsedMilliseconds + "ms");
            timer.Stop();
            MInput.Disabled = false;

            timer = Stopwatch.StartNew();
            AreaData.Load();
            Console.WriteLine(" - LEVELS LOAD: " + timer.ElapsedMilliseconds + "ms");
            timer.Stop();

            Console.WriteLine("DONE LOADING (in " + Celeste.LoadTimer.ElapsedMilliseconds + "ms)");
            Celeste.LoadTimer.Stop();
            Celeste.LoadTimer = null;
            loaded = true;
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchGameLoaderIntroRoutine] // ... except for manually manipulating the method via MonoModRules
        public extern new IEnumerator IntroRoutine();

        private static Scene _GetNextScene(Overworld.StartMode startMode, HiresSnow snow) {
            bool transitionToModUpdater = false;

            if (CoreModule.Settings.AutoUpdateModsOnStartup) {
                if (!ModUpdaterHelper.IsAsyncUpdateCheckingDone()) {
                    // update checking is not done yet.
                    // transition to mod updater screen to display the "checking for updates" message.
                    transitionToModUpdater = true;
                } else {
                    SortedDictionary<ModUpdateInfo, EverestModuleMetadata> modUpdates = ModUpdaterHelper.GetAsyncLoadedModUpdates();
                    if (modUpdates != null && modUpdates.Count != 0) {
                        // update checking is done, and updates are available.
                        // transition to mod updater screen in order to install the updates
                        transitionToModUpdater = true;
                    }
                }
            }

            if (transitionToModUpdater) {
                return new AutoModUpdater(snow);
            } else {
                return new OverworldLoader(startMode, snow);
            }
        }
    }
}
