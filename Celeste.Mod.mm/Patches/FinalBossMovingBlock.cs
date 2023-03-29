using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System.Runtime.CompilerServices;

namespace Celeste {
    class patch_FinalBossMovingBlock : FinalBossMovingBlock {

        internal Vector2 movementCounter {
            [MonoModLinkTo("Celeste.Platform", "get__movementCounter")] get;
        }

        public patch_FinalBossMovingBlock(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModPatch("<>c__DisplayClass14_0")]
        class patch_MoveSequenceLambdas {

            [MonoModPatch("<>4__this")]
            private patch_FinalBossMovingBlock _this = default;
            private Vector2 from = default, to = default;

            [MonoModReplace]
            [MonoModPatch("<MoveSequence>b__0")]
            public void TweenUpdateLambda(Tween t) {
                // Patch this to always behave like XNA
                // This is absolutely hecking ridiculous and a perfect example of why we want to switch to .NET Core
                // The Y member gets downcast but not the X one because of JIT jank
                double lerpX = from.X + ((double) to.X - from.X) * t.Eased, lerpY = from.Y + ((double) to.Y - from.Y) * t.Eased;
                _this.MoveH((float) (lerpX - _this.Position.X - _this.movementCounter.X));
                _this.MoveV((float) ((double) JITBarrier((float) lerpY) - _this.Position.Y - _this.movementCounter.Y));
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static float JITBarrier(float v) => v;

        }

    }
}