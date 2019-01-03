using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Entities {
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
