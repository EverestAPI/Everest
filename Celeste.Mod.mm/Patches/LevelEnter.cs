#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Celeste.Mod;
using MonoMod;
using System.Collections;
using Monocle;

namespace Celeste {
    class patch_LevelEnter : Scene {

        public static string ErrorMessage;

        private Session session;

        private Postcard postcard;

        extern public static void orig_Go(Session session, bool fromSaveData);
        public static void Go(Session session, bool fromSaveData) {
            orig_Go(session, fromSaveData);
            Everest.Events.Level.Enter(session, fromSaveData);
        }

        private extern IEnumerator orig_Routine();
        private IEnumerator Routine() {
            if (ErrorMessage != null) {
                return ErrorRoutine();
            }

            if (AreaData.Get(session.Area) == null) {
                string message = Dialog.Get("postcard_levelgone");
                message = message.Replace("((player))", SaveData.Instance.Name);
                message = message.Replace("((sid))", session.Area.GetSID());
                ErrorMessage = message;
                return ErrorRoutine();
            }

            return orig_Routine();
        }

        private IEnumerator ErrorRoutine() {
            yield return 1f;

            Add(postcard = new Postcard(ErrorMessage));
            yield return postcard.DisplayRoutine();

            SaveData.Instance.CurrentSession = new Session(AreaKey.Default);
            SaveData.Instance.LastArea = AreaKey.None;
            Engine.Scene = new OverworldLoader(Overworld.StartMode.AreaComplete);
            ErrorMessage = null;
        }

    }
    public static class LevelEnterExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        public static string ErrorMessage {
            get {
                return patch_LevelEnter.ErrorMessage;
            }
            set {
                patch_LevelEnter.ErrorMessage = value;
            }
        }

    }
}
