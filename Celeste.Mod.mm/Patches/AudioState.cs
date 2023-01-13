#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using MonoMod;

namespace Celeste {
    class patch_AudioState : AudioState {

        public float AmbienceVolume;

        [MonoModConstructor]
        [MonoModReplace]
        public void ctor() {
            AmbienceVolume = 1f;
        }

        public extern void orig_ctor(AudioTrackState music, AudioTrackState ambience);
        [MonoModConstructor]
        public void ctor(AudioTrackState music, AudioTrackState ambience) {
            orig_ctor(music, ambience);
            AmbienceVolume = 1f;
        }

        public extern void orig_ctor(string music, string ambience);
        [MonoModConstructor]
        public void ctor(string music, string ambience) {
            orig_ctor(music, ambience);
            AmbienceVolume = 1f;
        }

        public extern void orig_Apply(bool forceSixteenthNoteHack = false);
        public new void Apply(bool forceSixteenthNoteHack = false) {
            orig_Apply(forceSixteenthNoteHack);
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
