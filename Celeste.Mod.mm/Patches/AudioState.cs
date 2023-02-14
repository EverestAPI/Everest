#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System.Xml.Serialization;

namespace Celeste {
    class patch_AudioState : AudioState {

        public float? AmbienceVolume;

        [XmlIgnore]
        public bool AmbienceVolumeSpecified { get { return AmbienceVolume.HasValue; } }

        public extern void orig_Apply(bool forceSixteenthNoteHack = false);
        public new void Apply(bool forceSixteenthNoteHack = false) {
            orig_Apply(forceSixteenthNoteHack);
            if (AmbienceVolume.HasValue)
                Audio.CurrentAmbienceEventInstance?.setVolume(AmbienceVolume.Value);
        }

        public void Apply() {
            Apply(false);
        }

        public extern patch_AudioState orig_Clone();
        public new patch_AudioState Clone() {
            patch_AudioState audioState = orig_Clone();
            audioState.AmbienceVolume = AmbienceVolume;
            return audioState;
        }
    }
}
