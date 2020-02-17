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
using System.Collections;
using Celeste.Mod.Meta;
using System.Collections.ObjectModel;
using static Celeste.Mod.StrawberryRegistry;
using System.Reflection;

namespace Celeste {
    class patch_HeartGem : HeartGem {

        public patch_HeartGem(Vector2 position)
            : base(position) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        private extern IEnumerator orig_CollectRoutine(Player player);
        [PatchHeartGemCollectRoutine]
        private IEnumerator CollectRoutine(Player player) {
            Level level = Scene as Level;

            bool heartIsEnd = false;
            MapMetaModeProperties mapMetaModeProperties = (level != null) ? level.Session.MapData.GetMeta() : null;
            if (mapMetaModeProperties != null && mapMetaModeProperties.HeartIsEnd != null) {
                heartIsEnd = mapMetaModeProperties.HeartIsEnd.Value;
            }

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
                return meta.HeartIsEnd.Value;

            return value;
        }

        [MonoModIgnore] // don't change anything in the method...
        [PatchTotalHeartGemChecks] // except for replacing TotalHeartGems with TotalHeartGemsInVanilla through MonoModRules
        private extern void RegisterAsCollected(Level level, string poemID);
    }
}
