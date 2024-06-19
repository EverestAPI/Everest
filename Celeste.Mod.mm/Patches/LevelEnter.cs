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
                // We have encountered an error, so start the scene directly to display our error screen.
                Engine.Scene = new patch_LevelEnter(session, fromSaveData);
            } else {
                try {
                    if (!PlayCustomVignette(session, fromSaveData))
                        orig_Go(session, fromSaveData);

                    Everest.Events.Level.Enter(session, fromSaveData);
                } catch (Exception e) {
                    string sid = session?.Area.GetSID() ?? "???";
                    Logger.Log(LogLevel.Warn, "LevelEnter", $"Failed entering map {sid}");
                    Logger.LogDetailed(e);

                    ErrorMessage = Dialog.Get("postcard_levelloadfailed").Replace("((sid))", sid);
                    Engine.Scene = new patch_LevelEnter(session, fromSaveData);
                }
            }
        }

        public static patch_LevelEnter ForceCreate(Session session, bool fromSaveData) {
            return new patch_LevelEnter(session, fromSaveData);
        }

        public static bool PlayCustomVignette(Session session, bool fromSaveData) {
            bool playVignette = !fromSaveData && session.StartedFromBeginning;
            patch_AreaData area = patch_AreaData.Get(session);
            MapMetaCompleteScreen screen;
            MapMetaTextVignette text;

            if (playVignette && (screen = area.Meta?.LoadingVignetteScreen) != null && screen.Atlas != null) {
                Engine.Scene = new CustomScreenVignette(session, meta: screen);
                return true;
            } else if (playVignette && (text = area.Meta?.LoadingVignetteText) != null && text.Dialog != null) {
                if (Engine.Scene is not Overworld { Snow: HiresSnow snow }) {
                    snow = null;
                }

                Engine.Scene = new CustomTextVignette(session, text, snow);
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
                Logger.Log(LogLevel.Warn, "LevelEnter", $"Failed to find map");
                return ErrorRoutine(Dialog.Get("postcard_levelgone")
                    .Replace("((player))", SaveData.Instance.Name)
                    .Replace("((sid))", session.Area.GetSID()));
            }

            patch_AreaData areaData = patch_AreaData.Get(session);
            MapMeta areaMeta = areaData.Meta;

            string postCardDialogInfix = "";
            int areaModeIndex = 0;
            
            if (session.Area.Mode == AreaMode.BSide) {
                postCardDialogInfix = "_b";
                areaModeIndex = 1;
            } else if (session.Area.Mode == AreaMode.CSide) {
                postCardDialogInfix = "_c";
                areaModeIndex = 2;
            }

            string postCardDialog = $"{areaData.Name}{postCardDialogInfix}_postcard";

            if (areaMeta != null && areaData.LevelSet != "Celeste" &&
                Dialog.Has(postCardDialog) &&
                session.StartedFromBeginning && !fromSaveData &&
                (!SaveData.Instance.Areas[session.Area.ID].Modes[areaModeIndex].Completed || SaveData.Instance.DebugMode)) {
                return EnterWithPostcardRoutine(Dialog.Get(postCardDialog), areaMeta.PostcardSoundID);
            }

            return orig_Routine();
        }

        private IEnumerator ErrorRoutine(string message) {
            Audio.SetMusic(null);
            Audio.SetAmbience(null);

            yield return 1f;

            Add(postcard = new Postcard(message, "event:/ui/main/postcard_csides_in", "event:/ui/main/postcard_csides_out"));
            yield return postcard.DisplayRoutine();

            session = new Session((AreaData.Get(session) != null) ? session.Area : new AreaKey(1).SetSID(""));

            SaveData.Instance.CurrentSession = session;
            SaveData.Instance.LastArea = session.Area;
            Engine.Scene = new OverworldLoader(Overworld.StartMode.AreaQuit);
        }

        private IEnumerator EnterWithPostcardRoutine(string message, string soundId) {
            yield return 1f;

            Add(postcard = new patch_Postcard(message, soundId));
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

        /// <inheritdoc cref="patch_LevelEnter.ErrorMessage"/>
        [Obsolete("Use LevelEnter.ErrorMessage instead.")]
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
