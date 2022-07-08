using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_CassetteBlock : CassetteBlock {

        public patch_CassetteBlock(EntityData data, Vector2 offset, EntityID id)
            : base(data, offset, id) {
        }

        // 1.3.0.0 gets rid of the 2-arg ctor.
        // We're adding a new ctor, thus can't call the constructor without a small workaround.
        [MonoModLinkTo("Celeste.CassetteBlock", "System.Void .ctor(Celeste.EntityData,Microsoft.Xna.Framework.Vector2,Celeste.EntityID)")]
        [MonoModForceCall]
        [MonoModRemove]
        public extern void ctor(EntityData data, Vector2 offset, EntityID id);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset) {
            ctor(data, offset, new EntityID(data.Level.Name, data.ID));
        }

        [MonoModIgnore]
        [PatchCassetteBlockAwake]
        public override extern void Awake(Scene scene);
    }
}
