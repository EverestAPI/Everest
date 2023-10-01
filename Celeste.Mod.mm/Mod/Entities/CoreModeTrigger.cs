using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities {
    [CustomEntity("everest/coreModeTrigger", "cavern/coremodetrigger")]
    public class CoreModeTrigger : Trigger {
        private enum Modes {
            None,
            Hot,
            Cold,
            Toggle
        }

        private readonly Modes mode;
        private readonly bool playEffects;

        public CoreModeTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
            mode = data.Enum("mode", Modes.None);
            playEffects = data.Bool("playEffects", true);
        }

        public override void OnEnter(Player player) {
            Level level = Scene as Level;

            Session.CoreModes newMode = Session.CoreModes.None;

            if (mode == Modes.Toggle) {
                if (level.CoreMode == Session.CoreModes.Hot)
                    newMode = Session.CoreModes.Cold;
                else if (level.CoreMode == Session.CoreModes.Cold)
                    newMode = Session.CoreModes.Hot;
            } else {
                newMode = (Session.CoreModes) mode;
            }

            if (level.CoreMode == newMode)
                return;

            level.CoreMode = newMode;

            if (playEffects) {
                Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
                level.Flash(Color.White * 0.15f, true);
                Celeste.Freeze(0.05f);
            }
        }
    }
}
