using Monocle;
using System.Collections;

namespace Celeste.Mod.Entities {
    public class DialogCutscene : CutsceneEntity {

        private Player player;
        private string dialogID;
        private bool endLevel;

        public DialogCutscene(string dialogID, Player player, bool endLevel) {
            this.dialogID = dialogID;
            this.player = player;
            this.endLevel = endLevel;
        }

        public override void OnBegin(Level level) {
            Add(new Coroutine(Cutscene(level)));
        }

        private IEnumerator Cutscene(Level level) {
            player.StateMachine.State = 11;
            player.StateMachine.Locked = true;
            player.ForceCameraUpdate = true;
            yield return Textbox.Say(dialogID);

            EndCutscene(level, true);
        }

        public override void OnEnd(Level level) {
            player.StateMachine.Locked = false;
            player.StateMachine.State = 0;
            player.ForceCameraUpdate = false;

            if (WasSkipped)
                level.Camera.Position = player.CameraTarget;

            if (endLevel) {
                level.CompleteArea();
                player.StateMachine.State = Player.StDummy;
                RemoveSelf();
            }
        }

    }
}
