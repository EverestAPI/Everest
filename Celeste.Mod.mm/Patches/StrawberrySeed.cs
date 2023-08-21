using Microsoft.Xna.Framework;
using System.Linq;
using MonoMod;
using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

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

        [MonoModIgnore]
        [PatchPatchStrawberrySeedOnAllCollected]
        public new extern void OnAllCollected();

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
namespace MonoMod {
    /// <summary>
    /// Patches OnAllCollected to add a check if this.follower.Leader is null.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchStrawberrySeedOnAllCollected))]
    class PatchPatchStrawberrySeedOnAllCollectedAttribute : Attribute { }

    static partial class MonoModRules {

        public static void PatchStrawberrySeedOnAllCollected(ILContext context, CustomAttribute attrib) {
            ILCursor cursor = new(context);
            ILLabel beforeLoseFollower = cursor.DefineLabel();
            ILLabel afterLoseFollower = cursor.DefineLabel();

            // we want to add a null check on Leader before calling LoseFollower so we change:
            // this.follower.Leader.LoseFollower(this.follower);
            // to
            // this.follower.Leader?.LoseFollower(this.follower);

            // move cursor to the point where Leader is on top of the stack, and then duplicate it for the branch true
            cursor.GotoNext(MoveType.After, inst => inst.MatchLdfld("Celeste.Follower", "Leader"));
            cursor.Emit(OpCodes.Dup);
            
            // brach to the calling path if we are not null
            cursor.Emit(OpCodes.Brtrue, beforeLoseFollower);
            // on null discard duplicated value and jump over the calling path
            cursor.Emit(OpCodes.Pop);
            cursor.Emit(OpCodes.Br, afterLoseFollower);
            
            cursor.GotoNext(MoveType.Before, inst => inst.MatchLdarg(0));
            cursor.MarkLabel(beforeLoseFollower);
            
            cursor.GotoNext(MoveType.After, inst => inst.MatchCallvirt("Celeste.Leader", "LoseFollower"));
            cursor.MarkLabel(afterLoseFollower);
        }

    }
}
