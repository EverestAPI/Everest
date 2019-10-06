using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
