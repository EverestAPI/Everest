using MonoMod;

namespace Monocle {
    class patch_SineWave : SineWave {

        [MonoModLinkTo("Monocle.SineWave", "System.Void .ctor(System.Single,System.Single)")]
        [MonoModForceCall]
        [MonoModRemove]
        public extern void ctor(float frequency, float offset = 0f);

        // Make this constructor signature accessible to older mods.
        [MonoModConstructor]
        public void ctor(float frequency) {
            ctor(frequency, 0f);
        }
    }
}
