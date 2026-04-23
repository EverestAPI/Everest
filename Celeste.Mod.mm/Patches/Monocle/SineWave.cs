using MonoMod;
using System;

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

        [MonoModReplace]
        public new SineWave Randomize() {
            Counter = (float) ((double) Calc.Random.NextFloat() * (double) (float) (Math.PI * 2f) * 2.0);
            return this;
        }

    }
}
