#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;

namespace Celeste {
    class patch_AudioState : AudioState {

        public float AmbienceVolume;

        [MonoModConstructor]
        public patch_AudioState() {
            AmbienceVolume = 1f;
        }

        [MonoModConstructor]
        public patch_AudioState(AudioTrackState music, AudioTrackState ambience) {
            if (music != null) {
                Music = music.Clone();
            }
            if (ambience != null) {
                Ambience = ambience.Clone();
            }
            AmbienceVolume = 1f;
        }

        [MonoModConstructor]
        public patch_AudioState(string music, string ambience) {
            Music.Event = music;
            Ambience.Event = ambience;
            AmbienceVolume = 1f;
        }

        public extern void orig_Apply(bool forceSixteenthNoteHack = false);
        public new void Apply(bool forceSixteenthNoteHack = false) {
            orig_Apply();
            Audio.CurrentAmbienceEventInstance?.setVolume(AmbienceVolume);
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
