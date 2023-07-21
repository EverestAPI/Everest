using MonoMod;
using System;

namespace Monocle {
    class patch_SineWave : SineWave {

        private float counter;

        public new float Value { [MonoModIgnore] get; [MonoModIgnore] private set; }
        public new float ValueOverTwo { [MonoModIgnore] get; [MonoModIgnore] private set; }
        public new float TwoValue { [MonoModIgnore] get; [MonoModIgnore] private set; }

        public new float Counter {
            [MonoModReplace] get => counter;
            [MonoModReplace] set {
                // Fix TAS desyncs by patching .NET FW float jank
                counter = (float) (((double) value + (double) (float) (Math.PI * 8f)) % (double) (float) (Math.PI * 8f));
                Value = (float) Math.Sin(counter);
                ValueOverTwo = (float) Math.Sin(counter / 2f);
                TwoValue = (float) Math.Sin(counter * 2f);
            }
        }

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
            // Fix TAS desyncs by patching .NET FW float jank
            Counter = (float) ((double) Calc.Random.NextFloat() * (double) (float) (Math.PI * 2f) * 2.0);
            return this;
        }

        [MonoModReplace]
        public override void Update() {
            // Fix TAS desyncs by patching .NET FW float jank
            Counter += (float) ((double) (float) (Math.PI * 2f) * (double) Frequency * (double) Rate * (double) (UseRawDeltaTime ? Engine.RawDeltaTime : Engine.DeltaTime));
            if (OnUpdate != null)
                OnUpdate(Value);
        }

    }
}
