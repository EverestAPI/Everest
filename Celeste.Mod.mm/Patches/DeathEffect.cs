using Microsoft.Xna.Framework;
using MonoMod;

namespace Celeste {
    class patch_DeathEffect : DeathEffect {

        public patch_DeathEffect(Color color, Vector2 offset)
            : base(color, offset) { }

        [MonoModIgnore]
        [PatchDeathEffectUpdate]
        public override extern void Update();

        [MonoModReplace]
        public override void Render() {
            if (Entity != null)
                Draw(Entity.Position + Position, Color, Percent);
        }
    }
}
