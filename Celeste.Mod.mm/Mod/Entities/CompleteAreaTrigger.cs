using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities {
    [CustomEntity("everest/completeAreaTrigger", "outback/completeareatrigger")]
    public class CompleteAreaTrigger : Trigger {

        public CompleteAreaTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
        }

        public override void OnEnter(Player player) {
            base.OnEnter(player);

            (Scene as Level).CompleteArea();
            player.StateMachine.State = Player.StDummy;

            RemoveSelf();
        }

    }
}
