using MonoMod;

namespace Celeste {
    class patch_Postcard : Postcard {

        public patch_Postcard(string msg, int area)
            : base(msg, area) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        // 1.3.0.0 gets rid of the 1-arg ctor.
        // We're adding a new ctor, thus can't call the constructor (Celeste.Postcard::.ctor) without a small workaround.
        [MonoModLinkTo("Celeste.Postcard", "System.Void .ctor(System.String,System.String,System.String)")]
        [MonoModForceCall]
        [MonoModRemove]
        public extern void ctor(string msg, string sfxEventIn, string sfxEventOut);
        [MonoModConstructor]
        public void ctor(string msg) {
            ctor(msg, "event:/ui/main/postcard_csides_in", "event:/ui/main/postcard_csides_out");
        }

        // 2-arg ctor parsing custom postcard sound IDs
        [MonoModConstructor]
        [MonoModRemove]
        public patch_Postcard(string msg, string soundId) 
            : base(msg, soundId, soundId) {
            // no-op. This ctor is only used to make the compiler work in Celeste.LevelEnter
        }

        [MonoModConstructor]
        public void ctor(string msg, string soundId) {
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

            ctor(msg, prefix + "_in", prefix + "_out");
        }

    }
}
