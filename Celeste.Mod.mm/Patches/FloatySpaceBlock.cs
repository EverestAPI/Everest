using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Celeste {
    class patch_FloatySpaceBlock : FloatySpaceBlock {

        private float sineWave = default, dashEase = default, yLerp = default;
        private Vector2 dashDirection = default;

        public patch_FloatySpaceBlock(EntityData data, Vector2 offset)
            : base(data, offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        //Might not be 100% accurate (I didn't check .NET Framework JIT output assembly for this), but good enough for now to make the TAS resync
        [MonoModReplace]
        private void MoveToTarget() {
            float sineVal = (float) (JITBarrier(4.0f) * (double) Math.Sin(sineWave)); //We can't use 4.0 directly because mods might look for this constant
            Vector2 dashVec = Calc.YoYo(Ease.QuadIn(dashEase)) * dashDirection * 8f; //No doubles here cause vector scalar multiplication acts like a JIT barrier
            for (int i = 0; i < 2; i++) {
                foreach (KeyValuePair<Platform, Vector2> move in Moves) {
                    Platform platform = move.Key;
                    bool hasRider = false;

                    JumpThru jumpThru = platform as JumpThru;
                    Solid solid = platform as Solid;
                    if ((jumpThru != null && jumpThru.HasRider()) || (solid != null && solid.HasRider()))
                        hasRider = true;

                    if ((hasRider || i != 0) && (!hasRider || i != 1)) {
                        Vector2 moveVal = move.Value;
                        double yVal = (double) moveVal.Y + JITBarrier(12.0f) * (double) Ease.SineInOut(yLerp) + sineVal; //We can't use 12.0 directly because might mods look for this constant
                        platform.MoveToY((float) (yVal + dashVec.Y));
                        platform.MoveToX(moveVal.X + dashVec.X);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float JITBarrier(float v) => v;

    }
}