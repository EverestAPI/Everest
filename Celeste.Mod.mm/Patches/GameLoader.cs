#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste {
    class patch_GameLoader : GameLoader {

        // We're effectively in GameLoader, but still need to "expose" private fields to our mod.
        private Entity handler;
        private bool loaded;

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
        }

        public extern void orig_Update();
        public override void Update() {
            if (introRoutine != null && Input.Pause.Pressed) {
                if (Input.MenuDown.Check) {
                    Celeste.PlayMode = Celeste.PlayModes.Debug;
                    // Late-enable commands. This is normally set by Celeste.Initialize.
                    Engine.Commands.Enabled = true;
                }
                introRoutine.Cancel();
                introRoutine = null;
            }
            // If we canceled the intro routine and finished loading, change scene.
            if (introRoutine == null && loaded) {
                Engine.Scene = new OverworldLoader(Overworld.StartMode.Titlescreen, Snow);
            }

            // Note: You may instinctually call base.Update();
            // DON'T! The original method is orig_Update
            orig_Update();
        }

    }
}
