#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using MonoMod;
using System.Runtime.CompilerServices;

namespace Celeste {
    class patch_CS10_Farewell : CS10_Farewell {
        private Player player;

        public patch_CS10_Farewell(Player player)
            : base(player) {
            // no-op. MonoMod ignores this - we only need this to make the compiler shut up.
        }

        [MonoModPatch("<OnEnd>b__17_0")]
        [MonoModReplace]
        [CompilerGenerated]
        private void _OnEnd_b__17_0() {
            if (Level.Session.Area.GetLevelSet() == "Celeste") {
                Achievements.Register(Achievement.FAREWELL);
            }
            Level.TeleportTo(player, "end-cinematic", Player.IntroTypes.Transition);
        }
    }
}
