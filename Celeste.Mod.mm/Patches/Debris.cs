#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Microsoft.Xna.Framework;
using Monocle;
using System.Collections.Generic;

namespace Celeste {
    class patch_Debris : Debris {

        private Image image;

        public Debris Init(Vector2 pos, char tileset) {
            return Init(pos, tileset, true);
        }

        public extern Debris orig_Init(Vector2 pos, char tileset, bool playSound = true);
        public new Debris Init(Vector2 pos, char tileset, bool playSound) {
            patch_Debris debris = (patch_Debris) orig_Init(pos, tileset, playSound);

            if (((patch_Autotiler) GFX.FGAutotiler).TryGetCustomDebris(out string path, tileset)) {
                List<MTexture> textures = GFX.Game.GetAtlasSubtextures("debris/" + path);
                debris.image.Texture = Calc.Choose(Calc.Random, textures);
            }

            return debris;
        }

    }
}
