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

        private Session session;

        private Postcard postcard;

        extern public static void orig_Go(Session session, bool fromSaveData);

        public static void Go(Session session, bool fromSaveData) {
            orig_Go(session, fromSaveData);
            Everest.Events.LevelEnter.Go(session, fromSaveData);
        }

        private extern IEnumerator orig_Routine();
        private IEnumerator Routine() {
            if (AreaData.Get(session.Area) == null)
                return LevelGoneRoutine();
            return orig_Routine();
        }

        private IEnumerator LevelGoneRoutine() {
            yield return 1f;

            string message = Dialog.Get("postcard_levelgone");
            message = message.Replace("((player))", SaveData.Instance.Name);
            message = message.Replace("((sid))", session.Area.GetSID());
            Add(postcard = new Postcard(message));
            yield return postcard.DisplayRoutine();

            SaveData.Instance.CurrentSession = new Session(AreaKey.Default);
            SaveData.Instance.LastArea = AreaKey.None;
            Engine.Scene = new OverworldLoader(Overworld.StartMode.AreaComplete);
        }

    }
}
