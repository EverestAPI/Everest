using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Entities {
    [Tracked]
    [CustomEntity("everest/dialogTrigger", "dialog/dialogtrigger", "cavern/dialogtrigger")]
    public class DialogCutsceneTrigger : Trigger {

        private string dialogEntry;
        private bool triggered;
        private EntityID id;
        private bool noRespawnAfterUse;
        private bool triggerOnlyOnce;
        private bool ignoreIntroState;
        private bool endLevel;
        private int deathCount;

        public DialogCutsceneTrigger(EntityData data, Vector2 offset, EntityID entId)
            : base(data, offset) {
            dialogEntry = data.Attr("dialogId");
            noRespawnAfterUse = data.Bool("onlyOnce", true); // don't rename the EntityData name for backwards compat
            triggerOnlyOnce = data.Bool("triggerOnlyOnce", true);
            ignoreIntroState = data.Bool("ignoreIntroState", false);
            endLevel = data.Bool("endLevel", false);
            deathCount = data.Int("deathCount", -1);
            triggered = false;
            id = entId;
        }

        // do not remove OnEnter! doing so will break maps that rely on third-party triggers to start dialog cutscenes.
        // vanilla naturally calls OnEnter and OnStay in the same frame when entering the trigger,
        // which would mean that we don't need the OnEnter method.
        // however, sj's "in filtration" uses a Trigger Trigger (CrystallineHelper) to start a dialog cutscene;
        // it only calls OnEnter and not OnStay, so removing OnEnter will make the dialog not appear and cause a tas desync!
        public override void OnEnter(Player player) {
            TriggerCutscene(player);
        }

        public override void OnStay(Player player) {
            TriggerCutscene(player);
        }

        public override void OnLeave(Player player) {
            if (!triggerOnlyOnce)
                triggered = false;
        }

        private void TriggerCutscene(Player player) {
            if (Scene is not Level level)
                return;

            if (triggered)
                return;

            if (deathCount >= 0 && level.Session.DeathsInCurrentLevel != deathCount)
                return;

            if (ignoreIntroState && ((patch_Player) player).IsIntroState)
                return;

            // don't activate if the same dialog is already in progress
            if (DialogCutscene.IsInProgress(dialogEntry))
                return;

            triggered = true;

            Scene.Add(new DialogCutscene(dialogEntry, player, endLevel));

            if (noRespawnAfterUse) {
                // this flag is unused in vanilla and Everest, but mods may still make use of it,
                // so it remains here for backwards compatibility
                level.Session.SetFlag("DoNotLoad" + id, true);
                level.Session.DoNotLoad.Add(id);
            }
        }
    }
}
