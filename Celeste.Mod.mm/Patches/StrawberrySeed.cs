using Microsoft.Xna.Framework;
using System.Linq;

namespace Celeste {
    public class patch_StrawberrySeed : StrawberrySeed {

        // private fields in StrawberrySeed we need to declare again to make accessible to our patches.
#pragma warning disable CS0649 // used but never assigned
        private Follower follower;
#pragma warning restore CS0649

#pragma warning disable CS0414 // assigned but never used
        private bool finished;
#pragma warning restore CS0414 // assigned but never used

        public patch_StrawberrySeed(Strawberry strawberry, Vector2 position, int index, bool ghost)
            : base(strawberry, position, index, ghost) {

            // this constructor will be ignored
        }

#pragma warning disable CS0626 // extern method with no attribute
        private extern void orig_OnPlayer(Player player);
#pragma warning restore CS0626

        private void OnPlayer(Player player) {
            orig_OnPlayer(player);

            // check if all seeds have been collected in the same way vanilla does.
            if (Strawberry.Seeds.All(seed => ((patch_StrawberrySeed) seed).follower.HasLeader)) {
                // set all seeds as "finished". this is usually done on the first frame of the cutscene...
                // but that leaves you able to lose another seed, crashing the game when the cutscene starts.
                // setting "finished" right away prevents the seeds from being lost.
                foreach (StrawberrySeed seed in Strawberry.Seeds) {
                    ((patch_StrawberrySeed) seed).finished = true;
                }
            }
        }
    }
}
