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
            Everest.Events.Level.OnLoadLevel += OnLoadLevel;
        }

        public override void Unload() {
            Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
        }

        public void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
            Logger.Log("ghost", $"OnLoadLevel level: {level.Session.Level} playerIntro: {playerIntro} isFromLoader: {isFromLoader}");

            if (isFromLoader) {
                GhostComparison = null;
                GhostRecorder = null;
                SessionTransition = 0;

                if (!level.Session.StartedFromBeginning)
                    // We can't properly keep track of the transition count when we're starting from the middle.
                    return;
            }

            if (playerIntro != Player.IntroTypes.Respawn)
                SessionTransition++;
            Step(level);
        }

        public void Step(Level level) {
            if (!Settings.Enabled || SessionTransition <= 0)
                return;

            Logger.Log("ghost", $"Step level: {level.Session.Level} transition: {SessionTransition}");

            Player player = level.Tracker.GetEntity<Player>();

            if (GhostRecorder != null && (GhostRecorder.Entity != player || GhostRecorder.Data == null))
                GhostRecorder = null;
            if (GhostComparison != null && GhostComparison.Player != player) {
                GhostComparison.RemoveSelf();
                GhostComparison = null;
            }

            // If we've got a new IL PB, write the ghost.
            if (GhostRecorder != null && GhostRecorder.Data != null &&
                (GhostComparison?.Data == null || GhostComparison.Data.Frames.Count >= GhostRecorder.Data.Frames.Count))
                GhostRecorder.Data.Write();

            level.Add(GhostComparison = new Ghost(player));
            GhostComparison.Player = player;
            GhostComparison.Data = new GhostData(level.Session, SessionTransition).Read();
            GhostComparison.FrameIndex = 0;

            if (GhostRecorder == null || GhostRecorder.Entity != player)
                player.Add(GhostRecorder = new GhostRecorder());
            GhostRecorder.Data = new GhostData(level.Session, SessionTransition);
        }

    }
}
