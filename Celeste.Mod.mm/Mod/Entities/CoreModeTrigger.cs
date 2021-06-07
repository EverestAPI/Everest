using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities {
    [CustomEntity("everest/coreModeTrigger", "cavern/coremodetrigger")]
    public class CoreModeTrigger : Trigger {
        private readonly Session.CoreModes mode;

        public CoreModeTrigger(EntityData data, Vector2 offset) 
            : base(data, offset) {
            mode = data.Enum("mode", Session.CoreModes.None);
        }

        public override void OnEnter(Player player) {
            Level level = Scene as Level;
            if (level.CoreMode == mode)
                return;
            level.CoreMode = mode;
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            level.Flash(Color.White * 0.15f, true);
            Celeste.Freeze(0.05f);
        }
    }
}
