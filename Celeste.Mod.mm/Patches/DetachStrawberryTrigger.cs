#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Microsoft.Xna.Framework;
using System.Collections;

namespace Celeste {
    class patch_DetachStrawberryTrigger : DetachStrawberryTrigger {

        public patch_DetachStrawberryTrigger(EntityData data, Vector2 offset) : base(data, offset) {
            // no-op, ignored by MonoMod
        }

        // not my spelling mistake
        private extern IEnumerator orig_DetatchFollower(Follower follower);
        private IEnumerator DetatchFollower(Follower follower) {
            if (!follower.HasLeader) {
                yield break; // don't detach follower if follower is no longer following leader (e.g. because player died)
            }
            yield return new SwapImmediately(orig_DetatchFollower(follower));
        }
    }
}