#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Xna.Framework;

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

        public extern void orig_Update();
        [PatchStrawberryTrainCollectionOrder]
        public new void Update()
        {
            orig_Update();
        }

        // Patch interface-implemented methods
        [MonoModIgnore]
        [PatchInterface]
        public extern new void CollectedSeeds();
    }
}
