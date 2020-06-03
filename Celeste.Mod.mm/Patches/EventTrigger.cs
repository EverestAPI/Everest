using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod;

namespace Celeste {
    class patch_EventTrigger : EventTrigger {

        private patch_EventTrigger(EntityData data, Vector2 offset) : base(data, offset) {
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchEventTriggerOnEnter] // ... except for manually manipulating the method via MonoModRules
        public override extern void OnEnter(Player player);

        public static bool TriggerCustomEvent(EventTrigger trigger, Player player, string eventID) {
            return Everest.Events.EventTrigger.TriggerEvent(trigger, player, eventID);
        }
    }
}
