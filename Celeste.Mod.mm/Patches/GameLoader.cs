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
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;

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
            MainThreadHelper.Boost = 25;
            Stopwatch timer = Stopwatch.StartNew();

            Audio.Init();
            // Original code loads audio banks here.
            Settings.Instance.ApplyVolumes();
            audioLoaded = true;
            Console.WriteLine(" - AUDIO LOAD: " + timer.ElapsedMilliseconds + "ms");
            timer.Stop();

            GFX.Load();
            MTN.Load();
            GFX.LoadData();
            MTN.LoadData();

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

            timer = Stopwatch.StartNew();
            MainThreadHelper.Boost = 50;
            patch_VirtualTexture.WaitFinishFastTextureLoading();
            MainThreadHelper.Schedule(() => MainThreadHelper.Boost = 0).AsTask().Wait();
            // FIXME: There could be ongoing tasks which add to the main thread queue right here.
            Console.WriteLine(" - FASTTEXTURELOADING LOAD: " + timer.ElapsedMilliseconds + "ms");
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
            checkModSaveDataBackups();

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

        /// <summary>
        /// Initializes mod save data backups if required (if Everest was just updated past 2104).
        /// </summary>
        private static void checkModSaveDataBackups() {
            Version previousVersion;
            try {
                previousVersion = new Version(CoreModule.Settings.CurrentVersion);
            } catch {
                // oops, version is null or can't be parsed for any reason.
                previousVersion = new Version(0, 0, 0);
            }

            if (previousVersion < new Version(1, 2109, 0)) {
                // user just upgraded: create mod save data backups.
                // (this is very similar to OverworldLoader.CheckVariantsPostcardAtLaunch)
                Logger.Verbose("core", $"User just upgraded from version {previousVersion}: creating mod save data backups.");

                for (int i = 0; i < 3; i++) { // only the first 3 saves really matter.
                    if (!UserIO.Exists(SaveData.GetFilename(i))) {
                        continue;
                    }
                    SaveData saveData = UserIO.Load<SaveData>(SaveData.GetFilename(i), backup: false);
                    if (saveData != null) {
                        saveData.AfterInitialize();
                        UserIO.Save<ModSaveData>(SaveData.GetFilename(saveData.FileSlot) + "-modsavedata", UserIO.Serialize(new ModSaveData(saveData as patch_SaveData)));
                    }
                }
                UserIO.Close();
                SaveData.Instance = null;
            }
        }
    }
}

namespace MonoMod {
    /// <summary>
    /// Patch the GameLoader.IntroRoutine method instead of reimplementing it in Everest.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchGameLoaderIntroRoutine))]
    class PatchGameLoaderIntroRoutineAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchGameLoaderIntroRoutine(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_GetNextScene = method.DeclaringType.FindMethod("Monocle.Scene _GetNextScene(Celeste.Overworld/StartMode,Celeste.HiresSnow)");

            // The routine is stored in a compiler-generated method.
            method = method.GetEnumeratorMoveNext();

            bool found = false;

            Mono.Collections.Generic.Collection<Instruction> instrs = method.Body.Instructions;
            for (int instri = 0; instri < instrs.Count; instri++) {
                Instruction instr = instrs[instri];

                if (instr.OpCode == OpCodes.Newobj && (instr.Operand as MethodReference)?.GetID() == "System.Void Celeste.OverworldLoader::.ctor(Celeste.Overworld/StartMode,Celeste.HiresSnow)") {
                    instr.OpCode = OpCodes.Call;
                    instr.Operand = m_GetNextScene;
                    found = true;
                }
            }

            if (!found) {
                throw new Exception("Call to OverworldLoader::.ctor not found in " + method.FullName + "!");
            }
        }

    }
}
