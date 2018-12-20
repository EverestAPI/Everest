using System.Collections;
using Monocle;

namespace Celeste.Mod.Entities {
    public class DialogCutscene : CutsceneEntity {

        private Player player;
        private string dialogEntry;

        public DialogCutscene(string dialogID, Player playerEnt) {
            dialogEntry = dialogID;
            player = playerEnt;
        }

        public override void OnBegin(Level level) {
            Add(new Coroutine(Cutscene(level)));
        }

        private IEnumerator Cutscene(Level level) {
            player.StateMachine.State = 11;
            player.StateMachine.Locked = true;
            player.ForceCameraUpdate = true;
            yield return Textbox.Say(dialogEntry);

            EndCutscene(level, true);
        }

        public override void OnEnd(Level level) {
            player.StateMachine.Locked = false;
            player.StateMachine.State = 0;
            player.ForceCameraUpdate = false;

            if (WasSkipped)
                level.Camera.Position = player.CameraTarget;
        }

    }
}
