#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // The field is never used

using Microsoft.Xna.Framework;
using Monocle;
using MonoMod;

namespace Celeste {
    class patch_IntroCar : IntroCar {

        public bool hasRoadAndBarriers;

        public patch_IntroCar(EntityData data, Vector2 offset)
            : base(data.Position + offset) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        public extern void orig_ctor(EntityData data, Vector2 offset);
        [MonoModConstructor]
        public void ctor(EntityData data, Vector2 offset) {
            orig_ctor(data, offset);

            hasRoadAndBarriers = data.Bool("hasRoadAndBarriers", false);
        }

        public extern void orig_Added(Scene scene);
        public override void Added(Scene scene) {
            orig_Added(scene);

            Level level = scene as Level;
            if (level.Session.Area.GetLevelSet() == "Celeste")
                return;

            if (hasRoadAndBarriers) {
                level.Add(new IntroPavement(new Vector2(level.Bounds.Left, Y), (int) (X - level.Bounds.Left - 48f)) {
                    Depth = -10001
                });
                level.Add(new IntroCarBarrier(Position + new Vector2(32f, 0f), -10, Color.White));
                level.Add(new IntroCarBarrier(Position + new Vector2(41f, 0f), 5, Color.DarkGray));
            }
        }

        [MonoModIgnore]
        private extern float Easer(float v);

        [MonoModIgnore]
        private extern float EaserInverse(float v);

    }
}
