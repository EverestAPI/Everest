using Microsoft.Xna.Framework;

namespace Celeste.Mod.Entities {
    [CustomEntity("everest/changeInventoryTrigger")]
    public class ChangeInventoryTrigger : Trigger {
        private PlayerInventory inventory;

        public ChangeInventoryTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
            inventory = (PlayerInventory) typeof(PlayerInventory).GetField(data.Attr("inventory", "Default")).GetValue(null);
        }

        public override void OnEnter(Player player) {
            (Scene as Level).Session.Inventory = inventory;
        }
    }
}
