#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using MonoMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Celeste {
    class patch_HeartGem : HeartGem {

        private string fakeHeartDialog;
        private string keepGoingDialog;

        public patch_HeartGem(Vector2 position)
            : base(position) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(EntityData data, Vector2 offset);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset) {
            orig_ctor(data, offset);

            fakeHeartDialog = data.Attr("fakeHeartDialog", "CH9_FAKE_HEART");
            keepGoingDialog = data.Attr("keepGoingDialog", "CH9_KEEP_GOING");
        }

        [PatchHeartGemCollectRoutine]
        private extern IEnumerator orig_CollectRoutine(Player player);
        private IEnumerator CollectRoutine(Player player) {
            Level level = Scene as Level;

            bool heartIsEnd = false;
            MapMetaModeProperties mapMetaModeProperties = (level != null) ? level.Session.MapData.GetMeta() : null;
            if (mapMetaModeProperties != null && mapMetaModeProperties.HeartIsEnd != null) {
                heartIsEnd = mapMetaModeProperties.HeartIsEnd.Value;
            }

            heartIsEnd &= !IsFake;

            if (heartIsEnd) {
                List<IStrawberry> strawbs = new List<IStrawberry>();
                ReadOnlyCollection<Type> regBerries = StrawberryRegistry.GetBerryTypes();
                foreach (Follower follower in player.Leader.Followers) {

                    if (regBerries.Contains(follower.Entity.GetType()) && follower.Entity is IStrawberry) {
                        strawbs.Add(follower.Entity as IStrawberry);
                    }
                }
                foreach (IStrawberry strawb in strawbs) {
                    strawb.OnCollect();
                }
            }

            return orig_CollectRoutine(player);
        }

        private bool IsCompleteArea(bool value) {
            MapMetaModeProperties meta = (Scene as Level)?.Session.MapData.GetMeta();
            if (meta?.HeartIsEnd != null)
                return meta.HeartIsEnd.Value && !IsFake;

            return value;
        }

        [MonoModIgnore] // don't change anything in the method...
        [PatchTotalHeartGemChecks] // except for replacing TotalHeartGems with TotalHeartGemsInVanilla through MonoModRules
        private extern void RegisterAsCollected(Level level, string poemID);

        [MonoModIgnore] // don't change anything in the method...
        [PatchFakeHeartDialog] // except for replacing TotalHeartGems with TotalHeartGemsInVanilla through MonoModRules
        private extern IEnumerator DoFakeRoutineWithBird(Player player); 

    }
}
