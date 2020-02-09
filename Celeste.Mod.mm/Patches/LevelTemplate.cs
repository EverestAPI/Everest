using MonoMod;

namespace Celeste.Editor {
    class patch_LevelTemplate : LevelTemplate {
        public patch_LevelTemplate(LevelData data)
            : base(data) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModConstructor]
        [MonoModIgnore] // we don't want to change anything in the method...
        [PatchTrackableStrawberryCheck] // except manipulating it with MonoModRules
        public extern void ctor(LevelData data);
    }
}