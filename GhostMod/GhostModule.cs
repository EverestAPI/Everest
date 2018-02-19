using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Detour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Ghost {
    public class GhostModule : EverestModule {

        public static GhostModule Instance;

        public override Type SettingsType => typeof(GhostModuleSettings);
        public static GhostModuleSettings Settings => (GhostModuleSettings) Instance._Settings;

        public int SessionTransition;

        public Ghost GhostComparison;
        public GhostRecorder GhostRecorder;

        public GhostModule() {
            Instance = this;
        }

        public override void Load() {
            Everest.Events.LevelLoader.OnStartLevel += OnStartLevel;
            Everest.Events.Level.OnTransitionTo += OnTransition;
        }

        public override void Unload() {
            Everest.Events.LevelLoader.OnStartLevel -= OnStartLevel;
            Everest.Events.Level.OnTransitionTo -= OnTransition;
        }

        public void OnStartLevel(Level level) {
            GhostComparison = null;
            GhostRecorder = null;
            SessionTransition = 0;

            if (!level.Session.StartedFromBeginning)
                // We can't properly keep track of the transition count when we're starting from the middle.
                return;

            Next(level);
        }

        public void OnTransition(Level level, LevelData next, Vector2 direction) {
            Next(level);
        }

        public void Next(Level level) {
            if (!Settings.Enabled)
                return;

            // If we've got a new PB, write the ghost.
            if (GhostRecorder?.Data != null && (GhostComparison?.Data == null || GhostComparison.Data.Frames.Count >= GhostRecorder.Data.Frames.Count))
                GhostRecorder.Data.Write();
            SessionTransition++;

            // Add a new GhostComparison and update the GhostRecorder.
            Player player = level.Tracker.GetEntity<Player>();

            level.Add(GhostComparison = new Ghost(player, new GhostData(level.Session, SessionTransition).Read()));

            if (GhostRecorder == null)
                player.Add(GhostRecorder = new GhostRecorder());
            GhostRecorder.Data = new GhostData(level.Session, SessionTransition);
        }

    }
}
