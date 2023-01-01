using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// A trigger to change the current ambience track.
    ///
    /// Attributes:
    /// - `track`: the audio event to set the ambience to
    /// - `resetOnLeave`: if true, remembers what the ambience was before entering the trigger, and restores it after leaving
    /// </summary>
    [CustomEntity("everest/ambienceTrigger", "Sardine7/AmbienceTrigger")]
    public class AmbienceTrigger : Trigger {

        private string Track;
        private bool ResetOnLeave;

        private string oldTrack;

        public AmbienceTrigger(EntityData data, Vector2 offset) : base(data, offset) {
            Track = data.Attr("track");
            ResetOnLeave = data.Bool("resetOnLeave", defaultValue: false);
        }

        public override void OnEnter(Player player) {
            if (ResetOnLeave) {
                oldTrack = Audio.GetEventName(Audio.CurrentAmbienceEventInstance);
            }

            Session session = SceneAs<Level>().Session;
            session.Audio.Ambience.Event = SFX.EventnameByHandle(Track);
            session.Audio.Apply();
        }

        public override void OnLeave(Player player) {
            if (ResetOnLeave) {
                Session session = SceneAs<Level>().Session;
                session.Audio.Ambience.Event = oldTrack;
                session.Audio.Apply();
            }
        }
    }
}
