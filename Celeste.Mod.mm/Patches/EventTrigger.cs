#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

using Celeste.Mod;
using Microsoft.Xna.Framework;
using MonoMod;
using System.Collections.Generic;

namespace Celeste {
    class patch_EventTrigger : EventTrigger {

        private static HashSet<string> _LoadStrings; // Generated in MonoModRules.PatchEventTriggerOnEnter

        public delegate CutsceneEntity CutsceneLoader(EventTrigger trigger, Player player, string eventID);
        public static readonly Dictionary<string, CutsceneLoader> CutsceneLoaders = new Dictionary<string, CutsceneLoader>();

        private patch_EventTrigger(EntityData data, Vector2 offset) : base(data, offset) {
        }

        [MonoModIgnore] // We don't want to change anything about the method...
        [PatchEventTriggerOnEnter] // ... except for manually manipulating the method via MonoModRules
        public override extern void OnEnter(Player player);

        public static bool TriggerCustomEvent(EventTrigger trigger, Player player, string eventID) {
            if (Everest.Events.EventTrigger.TriggerEvent(trigger, player, eventID))
                return true;

            if (CutsceneLoaders.TryGetValue(eventID, out CutsceneLoader loader)) {
                CutsceneEntity loaded = loader(trigger, player, eventID);
                if (loaded != null) {
                    trigger.Scene.Add(loaded);
                    return true;
                }
            }

            if (!_LoadStrings.Contains(eventID)) {
                Logger.Log(LogLevel.Warn, "EventTrigger", $"Event '{eventID}' does not exist!");
                return true; //To a avoid hard crash on missing event
            }

            return false;
        }
    }
}
