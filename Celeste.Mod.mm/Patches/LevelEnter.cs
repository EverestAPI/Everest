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
using Celeste.Mod.Meta;

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
                string message = ErrorMessage;
                ErrorMessage = null;
                return ErrorRoutine(message);
            }

            if (AreaData.Get(session) == null) {
                string message = Dialog.Get("postcard_levelgone")
                    .Replace("((player))", SaveData.Instance.Name)
                    .Replace("((sid))", session.Area.GetSID())
                ;
                return ErrorRoutine(message);
            }

            return orig_Routine();
        }

        private IEnumerator ErrorRoutine(string message) {
            yield return 1f;

            Add(postcard = new Postcard(message));
            yield return postcard.DisplayRoutine();

            SaveData.Instance.CurrentSession = new Session(AreaKey.Default);
            SaveData.Instance.LastArea = AreaKey.None;
            Engine.Scene = new OverworldLoader(Overworld.StartMode.AreaComplete);
        }

        private class patch_BSideTitle : Entity {
            private string artist;
            private string album;

            public extern void orig_ctor_BSideTitle(Session session);
            [MonoModConstructor]
            public void ctor_BSideTitle(Session session) {
                orig_ctor_BSideTitle(session);

                AreaData area = AreaData.Get(session);
                if (string.IsNullOrEmpty(artist) || Dialog.Has(area.Name + "_remix_artist"))
                    artist = Dialog.Get(area.Name + "_remix_artist");
                if (string.IsNullOrEmpty(album) || Dialog.Has(area.Name + "_remix_album"))
                    album = Dialog.Get(area.Name + "_remix_album");
            }
        }

    }
    public static class LevelEnterExt {

        // Mods can't access patch_ classes directly.
        // We thus expose any new members through extensions.

        /// <summary>
        /// The error message to display when entering a level. Null if no error message should be presented.
        /// </summary>
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
