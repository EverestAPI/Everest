using MonoMod;

namespace Celeste {
    class patch_DeathEffect {
        [MonoModIgnore]
        [PatchDeathEffectUpdate]
        public extern void Update();
    }
}
