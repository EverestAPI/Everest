#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod;

namespace Celeste {
    [PatchStrawberryInterface]
    class patch_Strawberry : Strawberry {

        public patch_Strawberry(EntityData data, Vector2 offset, EntityID gid)
            : base(data, offset, gid) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_OnCollect();
        [PatchInterface]
        public new void OnCollect() {
            orig_OnCollect();
            // "Patch hook", because maintaining a pre-Everest MMHOOK is too much work.
            Everest.Discord.OnStrawberryCollect();
        }

        [MonoModIgnore]
        [PatchStrawberryTrainCollectionOrder]
        public extern void orig_Update();

        public new void Update() {
            orig_Update();
        }

        // Patch interface-implemented methods
        [MonoModIgnore]
        [PatchInterface]
        public extern new void CollectedSeeds();
    }
}
