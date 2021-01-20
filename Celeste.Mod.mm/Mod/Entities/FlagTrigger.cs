using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Entities {
    /// <summary>
    /// A general purpose flag setting trigger, to be used by custom maps.
    /// 
    /// Checks for the following new attributes:
    /// - `string flag`
    /// - `bool state`
    /// - `Modes mode` (default: `OnPlayerEnter`; available: `OnPlayerEnter, OnPlayerLeave, OnLevelStart`)
    /// - `bool only_once` (default: `false`)
    /// - `int death_count` (default: `-1`)
    /// </summary>
    [Tracked]
    [CustomEntity("everest/flagTrigger")]
    public class FlagTrigger : Trigger {

        private string flag;
        private bool state;
        private Modes mode;
        private bool onlyOnce;
        private int deathCount;

        private bool triggered = false;

        public FlagTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
            flag = data.Attr("flag");
            state = data.Bool("state");
            mode = data.Enum("mode", Modes.OnPlayerEnter);
            onlyOnce = data.Bool("only_once", false);
            deathCount = data.Int("death_count", -1);
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);
            if (mode == Modes.OnLevelStart)
                Trigger();
        }

        public override void OnEnter(Player player) {
            if (mode == Modes.OnPlayerEnter)
                Trigger();
        }

        public override void OnLeave(Player player) {
            if (mode == Modes.OnPlayerLeave)
                Trigger();
        }

        private void Trigger() {
            if (triggered)
                return;

            if (deathCount >= 0 && (Scene as Level).Session.DeathsInCurrentLevel != deathCount)
                return;

            (Scene as Level).Session.SetFlag(flag, state);

            if (onlyOnce)
                triggered = true;
        }

        private enum Modes {
            OnPlayerEnter,
            OnPlayerLeave,
            OnLevelStart
        }

    }
}
