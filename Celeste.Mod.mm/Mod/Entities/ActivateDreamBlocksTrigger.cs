using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Entities {
    [CustomEntity("everest/activateDreamBlocksTrigger")]
    class ActivateDreamBlocksTrigger : Trigger {
        private bool rumble;
        public ActivateDreamBlocksTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
            rumble = data.Bool("fullRoutine");
        }

        public override void OnEnter(Player player) {
            Level level = Scene as Level;
            if (!level.Session.Inventory.DreamDash) {
                level.Session.Inventory.DreamDash = true;
                foreach (DreamBlock dreamBlock in level.Tracker.GetEntities<DreamBlock>()) {
                    if (rumble)
                        dreamBlock.Add(new Coroutine(dreamBlock.Activate(), true));
                    else
                        dreamBlock.ActivateNoRoutine();
                }
            }
        }
    }
}
