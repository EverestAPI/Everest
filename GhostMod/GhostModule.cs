using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Detour;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public static string PathGhosts { get; internal set; }

        public List<Ghost> Ghosts = new List<Ghost>();
        public GhostRecorder GhostRecorder;

        public GhostModule() {
            Instance = this;
            
        }

        public override void Load() {
            PathGhosts = Path.Combine(Everest.PathSettings, "Ghosts");
            if (!Directory.Exists(PathGhosts))
                Directory.CreateDirectory(PathGhosts);

            Everest.Events.Level.OnLoadLevel += OnLoadLevel;
            Everest.Events.Level.OnExit += OnExit;
            Everest.Events.Player.OnDie += OnDie;
        }

        public override void Unload() {
            Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
            Everest.Events.Level.OnExit -= OnExit;
            Everest.Events.Player.OnDie -= OnDie;
        }

        public void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
            if (isFromLoader) {
                Ghosts.Clear();
                GhostRecorder?.RemoveSelf();
                GhostRecorder = null;
            }

            Step(level);
        }

        public void OnExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
            if (mode == LevelExit.Mode.Completed ||
                mode == LevelExit.Mode.CompletedInterlude) {
                Step(level);
            }
        }

        public void Step(Level level) {
            if (Settings.Mode == GhostModuleMode.Off)
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
        }

        public void OnDie(Player player) {
            if (GhostRecorder == null || GhostRecorder.Data == null)
                return;

            // This is hacky, but it works:
            // Check the stack trace for Celeste.Level+* <Pause>*
            // and throw away the data when we're just retrying.
            foreach (StackFrame frame in new StackTrace().GetFrames()) {
                MethodBase method = frame?.GetMethod();
                if (method == null || method.DeclaringType == null)
                    continue;
                if (!method.DeclaringType.FullName.StartsWith("Celeste.Level+") ||
                    !method.Name.StartsWith("<Pause>"))
                    continue;

                GhostRecorder.Data = null;
                return;
            }

            GhostRecorder.Data.Dead = true;
        }

    }
}
