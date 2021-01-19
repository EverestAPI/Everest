using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    public class patch_RidgeGate : RidgeGate {
        // make this private field accessible to our mod.
        private Vector2? node;
        public patch_RidgeGate(EntityData data, Vector2 offset) : base(data, offset) {
            // dummy constructor
        }

        [MonoModLinkTo("Celeste.Solid", "System.Void .ctor(Microsoft.Xna.Framework.Vector2,System.Single,System.Single,System.Boolean)")]
        [MonoModRemove]
        public extern void base_ctor(Vector2 position, float width, float height, bool safe);

        // create a new constructor with an extra parameter
        [MonoModConstructor]
        public void ctor(Vector2 position, float width, float height, Vector2? node, string texture) {
            base_ctor(position, width, height, safe: true);
            this.node = node;
            Add(new Image(GFX.Game[texture]));
        }

        // wire the EntityData, Vector2 constructor to the new constructor with the extra "texture" parameter
        [MonoModConstructor]
        [MonoModReplace]
        public void ctor(EntityData data, Vector2 offset) {
            ctor(data.Position + offset, data.Width, data.Height, data.FirstNodeNullable(offset), data.Attr("texture", data.Bool("ridge", true) ? "objects/ridgeGate" : "objects/farewellGate"));
        }

        // wire the existing "all settings" constructor to the new constructor with the extra "texture" parameter
        [MonoModConstructor]
        [MonoModReplace]
        public void ctor(Vector2 position, float width, float height, Vector2? node, bool ridgeImage = true) {
            ctor(position, width, height, node, ridgeImage ? "objects/ridgeGate" : "objects/farewellGate");
        }

        // backwards compatibility with 1.3.1.2
        [MonoModConstructor]
        [MonoModReplace]
        public void ctor(Vector2 position, float width, float height, Vector2? node) {
            ctor(position, width, height, node, "objects/ridgeGate");
        }
    }
}
