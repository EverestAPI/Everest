using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities {
    [CustomEntity("everest/coreModeTrigger", "cavern/coremodetrigger")]
    public class CoreModeTrigger : Trigger {
        private readonly Session.CoreModes mode;
        private readonly bool playEffects;

        public CoreModeTrigger(EntityData data, Vector2 offset) 
            : base(data, offset) {
            mode = data.Enum("mode", Session.CoreModes.None);
            playEffects = data.Bool("playEffects", true);
        }

        public override void OnEnter(Player player) {
            Level level = Scene as Level;
            if (level.CoreMode == mode)
                return;
            level.CoreMode = mode;
            if (playEffects) {
                Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
                level.Flash(Color.White * 0.15f, true);
                Celeste.Freeze(0.05f);
            }
        }
    }
}
