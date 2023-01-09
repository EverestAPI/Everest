using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// A trigger to change the volume of the current ambience track.
    /// </summary>
    [CustomEntity("everest/ambienceVolumeTrigger", "MaxHelpingHand/AmbienceVolumeTrigger")]
    public class AmbienceVolumeTrigger : Trigger {

        private float from;
        private float to;
        private PositionModes positionMode;

        public AmbienceVolumeTrigger(EntityData data, Vector2 offset) : base(data, offset) {
            from = data.Float("from");
            to = data.Float("to");
            positionMode = data.Enum("direction", PositionModes.NoEffect);
        }

        public override void OnStay(Player player) {
            patch_AudioState audioState = (patch_AudioState) SceneAs<Level>().Session.Audio;

            audioState.AmbienceVolume = Calc.ClampedMap(GetPositionLerp(player, positionMode), 0f, 1f, from, to);
            audioState.Apply();
        }
    }
}
