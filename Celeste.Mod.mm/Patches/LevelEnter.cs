#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

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

            Add(postcard = new Postcard(message, "event:/ui/main/postcard_csides_in", "event:/ui/main/postcard_csides_out"));
            yield return postcard.DisplayRoutine();

            SaveData.Instance.CurrentSession = new Session(AreaKey.Default);
            SaveData.Instance.LastArea = AreaKey.None;
            Engine.Scene = new OverworldLoader(Overworld.StartMode.AreaComplete);
        }

        private class patch_BSideTitle : Entity {
            private string artist;
            private string album;

            public extern void orig_ctor(Session session);
            [MonoModConstructor]
            public void ctor(Session session) {
                // Initialize the artist. If we are in a vanilla level, it will be replaced afterwards.
                AreaData area = AreaData.Get(session);
                artist = Dialog.Get(area.Name + "_remix_artist");

                orig_ctor(session);

                // Replace the album if defined in the language file.
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
