#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Celeste.Mod.Entities;
using Celeste.Mod.Meta;
using Monocle;
using MonoMod;
using System;
using System.Collections;

namespace Celeste {
    // LevelEnter has a private .ctor
    class patch_LevelEnter : Scene {

        /// <summary>
        /// The error message to display when entering a level. Null if no error message should be presented.
        /// </summary>
        public static string ErrorMessage;

        private Session session;

        private Postcard postcard;

        private bool fromSaveData;

        private patch_LevelEnter(Session session, bool fromSaveData) {
            // no-op.
        }

        extern public static void orig_Go(Session session, bool fromSaveData);
        public static void Go(Session session, bool fromSaveData) {
            if (ErrorMessage != null) {
                // We are entering the error screen. Invoke the original method which will display it.
                orig_Go(session, fromSaveData);
            } else {
                try {
                    if (!PlayCustomVignette(session, fromSaveData))
                        orig_Go(session, fromSaveData);

                    Everest.Events.Level.Enter(session, fromSaveData);
                } catch (Exception e) {
                    Logger.Log(LogLevel.Warn, "misc", $"Failed entering area {session.Area}");
                    Logger.LogDetailed(e);

                    string message = Dialog.Get("postcard_levelloadfailed")
                        .Replace("((player))", SaveData.Instance.Name)
                        .Replace("((sid))", session.Area.GetSID())
                    ;

                    LevelEnterExt.ErrorMessage = message;
                    LevelEnter.Go(new Session(new AreaKey(1)), false);
                }
            }
        }

        public static patch_LevelEnter ForceCreate(Session session, bool fromSaveData) {
            return new patch_LevelEnter(session, fromSaveData);
        }

        public static bool PlayCustomVignette(Session session, bool fromSaveData) {
            bool playVignette = !fromSaveData && session.StartedFromBeginning;
            AreaData area = AreaData.Get(session);
            MapMetaCompleteScreen screen;
            MapMetaTextVignette text;

            if (playVignette && (screen = area.GetMeta()?.LoadingVignetteScreen) != null && screen.Atlas != null) {
                Engine.Scene = new CustomScreenVignette(session, meta: screen);
                return true;
            } else if (playVignette && (text = area.GetMeta()?.LoadingVignetteText) != null && text.Dialog != null) {
                HiresSnow snow = null;
                if (Engine.Scene is Overworld)
                    snow = (Engine.Scene as Overworld).Snow;

                if (snow != null && text.SnowDirection != null)
                    snow.Direction = text.SnowDirection;

                Engine.Scene = new CustomTextVignette(session, text.Dialog, snow);
                return true;
            }

            return false;
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

            AreaData areaData = AreaData.Get(session);
            MapMeta areaMeta = areaData.GetMeta();
            if (areaMeta != null && areaData.GetLevelSet() != "Celeste" &&
                Dialog.Has(areaData.Name + "_postcard") &&
                session.StartedFromBeginning && !fromSaveData &&
                session.Area.Mode == AreaMode.Normal &&
                (!SaveData.Instance.Areas[session.Area.ID].Modes[0].Completed || SaveData.Instance.DebugMode)) {
                return EnterWithPostcardRoutine(Dialog.Get(areaData.Name + "_postcard"), areaMeta.PostcardSoundID);
            }

            return orig_Routine();
        }

        private IEnumerator ErrorRoutine(string message) {
            yield return 1f;

            Add(postcard = new Postcard(message, "event:/ui/main/postcard_csides_in", "event:/ui/main/postcard_csides_out"));
            yield return postcard.DisplayRoutine();

            SaveData.Instance.CurrentSession = session;
            SaveData.Instance.LastArea = session.Area;
            if (AreaData.Get(session.Area) == null) {
                // the area we are returning to doesn't exist anymore. return to Prologue instead.
                SaveData.Instance.LastArea = AreaKey.Default;
            }
            Engine.Scene = new OverworldLoader(Overworld.StartMode.AreaQuit);
        }

        private IEnumerator EnterWithPostcardRoutine(string message, string soundId) {
            yield return 1f;

            if (string.IsNullOrEmpty(soundId))
                soundId = "csides";

            string prefix;
            if (soundId.StartsWith("event:/")) {
                // sound ID is a FMOD event, take it as is.
                prefix = soundId;
            } else if (soundId == "variants") {
                // sound ID is "variants", this is a special case since it is in the new_content bank.
                prefix = "event:/new_content/ui/postcard_variants";
            } else {
                // if a number, use event:/ui/main/postcard_ch{number}
                // if not, use event:/ui/main/postcard_{text}
                prefix = "event:/ui/main/postcard_";
                if (int.TryParse(soundId, out _))
                    prefix += "ch";
                prefix += soundId;
            }

            Add(postcard = new Postcard(message, prefix + "_in", prefix + "_out"));
            yield return postcard.DisplayRoutine();

            IEnumerator inner = orig_Routine();
            while (inner.MoveNext())
                yield return inner.Current;
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

        /// <inheritdoc cref="patch_LevelEnter.ErrorMessage"/>
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