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

    }
}
