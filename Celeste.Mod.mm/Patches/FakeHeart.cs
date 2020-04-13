using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    public class patch_FakeHeart : FakeHeart {
        public patch_FakeHeart(Vector2 position)
            : base(position) {
            // dummy constructor
        }

        // -1 is the vanilla (random) color.
        private AreaMode color;

        [MonoModConstructor]
        [MonoModIgnore]
        public extern void ctor(Vector2 position);

        [MonoModConstructor]
        [MonoModReplace]
        public void ctor(EntityData data, Vector2 offset) {
            ctor(data.Position + offset);

            color = data.Enum<AreaMode>("color", (AreaMode) (-1));
        }

        [MonoModIgnore]
        [PatchFakeHeartColor] // adds a call to _getCustomColor to override the random color
        public extern override void Awake(Scene scene);

        private static AreaMode _getCustomColor(AreaMode vanillaColor, patch_FakeHeart self) {
            return self.color != (AreaMode) (-1) ? self.color : vanillaColor;
        }
    }
}
