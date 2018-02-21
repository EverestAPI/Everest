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

        public List<Ghost> Ghosts = new List<Ghost>();
        public GhostRecorder GhostRecorder;

        public GhostModule() {
            Instance = this;
        }

        public override void Load() {
            Everest.Events.Level.OnLoadLevel += OnLoadLevel;
            Everest.Events.Player.OnDie += OnDie;
        }

        public override void Unload() {
            Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
            Everest.Events.Player.OnDie -= OnDie;
        }

        public void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
            if (isFromLoader) {
                Ghosts.Clear();
                GhostRecorder = null;
            }

            Step(level);
        }

        public void Step(Level level) {
            if (!Settings.Enabled)
                return;

            string target = level.Session.Level;
            Logger.Log("ghost", $"Stepping into {level.Session.Area.GetSID()} {target}");

            Player player = level.Tracker.GetEntity<Player>();

            // Remove any dead ghosts (heh)
            for (int i = Ghosts.Count - 1; i > -1; --i) {
                Ghost ghost = Ghosts[i];
                if (ghost.Player != player)
                    ghost.RemoveSelf();
            }
            Ghosts.Clear();

            // Write the ghost, even if we haven't gotten an IL PB.
            // Maybe we left the level prematurely earlier?
            if (GhostRecorder?.Data != null) {
                GhostRecorder.Data.Target = target;
                GhostRecorder.Data.Write();
            }

            // Read and add all ghosts.
            GhostData.ForAllGhosts(level.Session, (i, ghostData) => {
                Ghost ghost = new Ghost(player, ghostData);
                level.Add(ghost);
                Ghosts.Add(ghost);
                return true;
            });

            if (GhostRecorder == null)
                player.Add(GhostRecorder = new GhostRecorder());
            GhostRecorder.Data = new GhostData(level.Session);
        }

        public void OnDie(Player player) {
            if (GhostRecorder?.Data != null)
                GhostRecorder.Data.Dead = true;
        }

    }
}
