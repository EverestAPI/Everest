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

        private string cmdTASPath;

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

        public override bool ParseArg(string arg, Queue<string> args) {
            if (arg == "--tas" && args.Count >= 1) {
                cmdTASPath = args.Dequeue();
                Logger.Log("ghost", $"Found --tas argument: {cmdTASPath}");
                return true;
            }

            return false;
        }

        public override void Initialize() {
            if (cmdTASPath != null) {
                cmdTASPath = Path.Combine(Everest.PathSettings, "Ghosts", cmdTASPath + ".oshiro");
                Logger.Log("ghost", $"Loading TAS input file: {cmdTASPath}");
                GhostData data = new GhostData(cmdTASPath).Read();
                if (data != null) {
                    Logger.Log("ghost", "Loaded, adding GhostReplayer component.");
                    Celeste.Instance.Components.Add(new GhostInputReplayer(Celeste.Instance, data));
                } else {
                    Logger.Log("ghost", "TAS input file failed loading.");
                }
            }
        }

        public void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
            if (isFromLoader) {
                Ghosts.Clear();
                GhostRecorder?.RemoveSelf();
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

            // Write the ghost, even if we haven't gotten an IL PB.
            // Maybe we left the level prematurely earlier?
            if (GhostRecorder?.Data != null) {
                GhostRecorder.Data.Target = target;
                GhostRecorder.Data.Write();
            }

            // Remove any dead ghosts (heh)
            for (int i = Ghosts.Count - 1; i > -1; --i) {
                Ghost ghost = Ghosts[i];
                if (ghost.Player != player)
                    ghost.RemoveSelf();
            }
            Ghosts.Clear();

            // Read and add all ghosts.
            GhostData.ForAllGhosts(level.Session, (i, ghostData) => {
                Ghost ghost = new Ghost(player, ghostData);
                level.Add(ghost);
                Ghosts.Add(ghost);
                return true;
            });

            if (GhostRecorder != null)
                GhostRecorder.RemoveSelf();
            level.Add(GhostRecorder = new GhostRecorder(player));
            GhostRecorder.Data = new GhostData(level.Session);
            GhostRecorder.Data.Name = Settings.Name;

            if (Ghosts.Count > 0)
                level.Add(new GhostName(player, !string.IsNullOrEmpty(Settings.Name) ? Settings.Name : "YOU"));
        }

        public void OnDie(Player player) {
            if (GhostRecorder?.Data != null)
                GhostRecorder.Data.Dead = true;
        }

    }
}
